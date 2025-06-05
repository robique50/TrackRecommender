using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using System.Text.Json;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<TrailImportService> logger)
    {
        private readonly AppDbContext _context = context;
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("OverpassAPI");
        private readonly ILogger<TrailImportService> _logger = logger;
        private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 4326);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private int _newTrailCounter = 0;
        private int _updatedTrailCounter = 0;
        private Dictionary<int, Region> _regionsCache = [];
        private readonly HashSet<long> _processedOsmRelationIdsThisRun = [];

        private const int OVERPASS_QUERY_TIMEOUT_SECONDS = 1800;
        private const int MAX_OVERPASS_RETRIES = 2;
        private const int OVERPASS_RETRY_DELAY_SECONDS = 30;

        private const int DB_BATCH_SIZE = 50;

        private const double PRIMARY_CONNECTION_TOLERANCE = 0.0005;
        private const double CONSERVATIVE_SIMPLIFY_TOLERANCE = 0.00002;
        private const double DUPLICATE_POINT_CLEANUP_TOLERANCE = 0.000001;

        private const int OVERPASS_GEOMETRY_REQUEST_DELAY_MS = 1000;
        private const int RELATION_GEOMETRY_BATCH_SIZE = 10;

        private const double MIN_TRAIL_DISTANCE_KM = 0.5;
        private const double MAX_TRAIL_DISTANCE_KM = 2000.0;

        public async Task ImportAllTrailsAsync(CancellationToken cancellationToken = default)
        {
            _newTrailCounter = 0;
            _updatedTrailCounter = 0;
            _processedOsmRelationIdsThisRun.Clear();

            try
            {
                await LoadRegionsCache(cancellationToken);

                await ImportHikingTrailsAsync(null, cancellationToken);

                _logger.LogInformation("Hiking trail import completed. New trails: {NewTrailsCount}, Updated trails: {UpdatedTrailsCount}", _newTrailCounter, _updatedTrailCounter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A critical error occurred during the hiking trail import process.");
            }
        }

        private async Task LoadRegionsCache(CancellationToken cancellationToken)
        {
            var regions = await _context.Regions
                .Where(r => r.Boundary != null && r.Boundary.IsValid && !r.Boundary.IsEmpty)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            _regionsCache = regions.ToDictionary(r => r.Id);
        }

        private async Task ImportHikingTrailsAsync(long? areaId, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;
            const string description = "Hiking Trails";

            var relationIds = await GetOverpassRelationIdsForRouteTypesAsync(areaId, ["hiking"], cancellationToken);
            if (relationIds.Count == 0)
            {
                return;
            }

            int processedRelationCount = 0;
            for (int i = 0; i < relationIds.Count; i += RELATION_GEOMETRY_BATCH_SIZE)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var idBatch = relationIds.Skip(i).Take(RELATION_GEOMETRY_BATCH_SIZE).ToList();
                if (idBatch.Count == 0) continue;

                string batchDescription = $"{description} (ID Batch {i / RELATION_GEOMETRY_BATCH_SIZE + 1})";
                OverpassResponse? batchResponse = null;

                for (int retry = 0; retry <= MAX_OVERPASS_RETRIES; retry++)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        var geometryQuery = BuildOverpassQueryForMultipleRelationGeometries(idBatch);
                        batchResponse = await ExecuteOverpassQueryAsync(geometryQuery, cancellationToken);
                        if (batchResponse != null) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Attempt {RetryAttempt}/{MaxRetries} to fetch geometry batch for {BatchDescription} failed.", retry + 1, MAX_OVERPASS_RETRIES, batchDescription);
                        if (retry < MAX_OVERPASS_RETRIES) await Task.Delay(TimeSpan.FromSeconds(OVERPASS_RETRY_DELAY_SECONDS), cancellationToken);
                        else _logger.LogError(ex, "All attempts failed for geometry batch {BatchDescription}.", batchDescription);
                    }
                }

                if (batchResponse?.Elements != null && batchResponse.Elements.Count != 0)
                {
                    await ProcessOverpassResponseAsync(batchResponse, batchDescription, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Could not retrieve or process geometries for relation ID batch starting with {FirstId} ({BatchDescription}).", idBatch.FirstOrDefault(), batchDescription);
                }

                processedRelationCount += idBatch.Count;
                _logger.LogInformation("Progress for {Description}: {ProcessedCount}/{TotalCount} relations' geometries fetched and processed.", description, processedRelationCount, relationIds.Count);

                if (i + RELATION_GEOMETRY_BATCH_SIZE < relationIds.Count && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(OVERPASS_GEOMETRY_REQUEST_DELAY_MS, cancellationToken);
                }
            }
        }

        private async Task<List<long>> GetOverpassRelationIdsForRouteTypesAsync(long? areaId, string[] routeTypes, CancellationToken cancellationToken)
        {
            var routeTypeFilter = string.Join("|", routeTypes);
            var query = BuildOverpassQueryForRelationIdsByRouteType(areaId, routeTypeFilter);

            OverpassResponse? response = null;
            for (int retry = 0; retry <= MAX_OVERPASS_RETRIES; retry++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    response = await ExecuteOverpassQueryAsync(query, cancellationToken);
                    if (response != null) break;
                }
                catch (Exception)
                {
                    if (retry < MAX_OVERPASS_RETRIES) await Task.Delay(TimeSpan.FromSeconds(OVERPASS_RETRY_DELAY_SECONDS), cancellationToken);
                }
            }

            if (response?.Elements == null)
            {
                return [];
            }

            var initialIds = response.Elements
                .Where(e => e.Type == "relation" && e.Id > 0)
                .Select(e => e.Id)
                .Distinct()
                .ToList();

            var finalIds = initialIds.Except(_processedOsmRelationIdsThisRun).ToList();
            return finalIds;
        }

        private static string BuildOverpassQueryForRelationIdsByRouteType(long? areaId, string routeTypeRegex)
        {
            var areaDefinition = areaId.HasValue ? $"area({areaId.Value})->.searchArea;" : @"area[""name""=""România""][""admin_level""=""2""]->.searchArea;";
            var routeFilterClause = $"[\"route\"~\"^({routeTypeRegex})$\"]";

            return $@"
                [out:json][timeout:{OVERPASS_QUERY_TIMEOUT_SECONDS}];
                {areaDefinition}
                (
                    relation(area.searchArea)[""type""=""route""]{routeFilterClause}[""name""];
                );
                out ids;";
        }

        private static string BuildOverpassQueryForMultipleRelationGeometries(List<long> relationIds)
        {
            if (relationIds.Count == 0) return string.Empty;
            var relationClauses = string.Join(";\n", relationIds.Select(id => $"relation({id})"));
            return $@"
                [out:json][timeout:{OVERPASS_QUERY_TIMEOUT_SECONDS}];
                (
                    {relationClauses};
                );
                (._;>>;);
                out geom;";
        }

        private async Task<OverpassResponse?> ExecuteOverpassQueryAsync(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }
            try
            {
                var content = new StringContent($"data={Uri.EscapeDataString(query)}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://overpass-api.de/api/interpreter") { Content = content };

                var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _logger.LogInformation("Received response status: {StatusCode} from Overpass API.", response.StatusCode);

                response.EnsureSuccessStatusCode();

                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                var overpassResponse = await JsonSerializer.DeserializeAsync<OverpassResponse>(responseStream, JsonOptions, cancellationToken);

                _logger.LogInformation("Successfully deserialized Overpass response. Elements received: {Count}", overpassResponse?.Elements?.Count ?? 0);
                return overpassResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Overpass query. Query:\n{Query}", query); throw;
            }
        }

        private async Task ProcessOverpassResponseAsync(OverpassResponse response, string sourceDescription, CancellationToken cancellationToken)
        {
            var relations = response.Elements
                .Where(e => e.Type == "relation" && e.Tags?.ContainsKey("name") == true && e.Id > 0 && !_processedOsmRelationIdsThisRun.Contains(e.Id))
                .ToList();

            if (relations.Count == 0)
            {
                return;
            }

            var waysDict = response.Elements
                .Where(e => e.Type == "way" && e.Id > 0 && e.Geometry != null && e.Geometry.Count != 0)
                .ToDictionary(w => w.Id);

            _logger.LogInformation("Processing {RelationCount} trail relations from {SourceDescription}.", relations.Count, sourceDescription);

            var newTrailsBatch = new List<Trail>();
            var updatedTrailsBatch = new List<Trail>();

            foreach (var relation in relations)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var (trailToPersist, isNew) = await CreateOrPrepareTrailFromRelationAsync(relation, waysDict, cancellationToken);
                    if (trailToPersist != null)
                    {
                        if (isNew) newTrailsBatch.Add(trailToPersist);
                        else updatedTrailsBatch.Add(trailToPersist);

                        _processedOsmRelationIdsThisRun.Add(relation.Id);
                    }

                    if (newTrailsBatch.Count + updatedTrailsBatch.Count >= DB_BATCH_SIZE)
                    {
                        await SaveAndClearBatchesAsync(newTrailsBatch, updatedTrailsBatch, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error processing OSM relation {OsmId} (Name: {Name}). Skipping.", relation.Id, relation.Tags?.GetValueOrDefault("name", "N/A"));
                }
            }
            await SaveAndClearBatchesAsync(newTrailsBatch, updatedTrailsBatch, cancellationToken);
        }

        private async Task SaveAndClearBatchesAsync(List<Trail> newTrails, List<Trail> updatedTrails, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested && newTrails.Count == 0 && updatedTrails.Count == 0)
            {
                newTrails.Clear(); updatedTrails.Clear();
                return;
            }

            bool changesMade = false;
            if (newTrails.Count != 0)
            {
                await _context.Trails.AddRangeAsync(newTrails, cancellationToken);
                _newTrailCounter += newTrails.Count;
                _logger.LogInformation("Added {Count} new trails to context for saving.", newTrails.Count);
                changesMade = true;
            }
            if (updatedTrails.Count != 0)
            {
                _updatedTrailCounter += updatedTrails.Count;
                _logger.LogInformation("Prepared {Count} trails for update in context.", updatedTrails.Count);
                changesMade = true;
            }

            if (changesMade)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Successfully saved batch of {NewCount} new and {UpdatedCount} updated trails to database.", newTrails.Count, updatedTrails.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save batch of trails to database.");
                }
            }
            newTrails.Clear();
            updatedTrails.Clear();
        }

        private async Task<(Trail? trail, bool isNew)> CreateOrPrepareTrailFromRelationAsync(OverpassElement relation, IReadOnlyDictionary<long, OverpassElement> waysDict, CancellationToken cancellationToken)
        {
            var tags = relation.Tags ?? [];
            var name = tags.GetValueOrDefault("name");

            var trailGeometry = BuildTrailGeometry(relation, waysDict);
            if (trailGeometry == null || trailGeometry.IsEmpty || trailGeometry.NumPoints < 2)
            {
                return (null, false);
            }

            var distance = CalculateDistance(trailGeometry);
            if (distance < MIN_TRAIL_DISTANCE_KM || distance > MAX_TRAIL_DISTANCE_KM)
            {
                return (null, false);
            }

            var finalGeometry = trailGeometry;
            if (finalGeometry.NumPoints > 300)
            {
                var simplifyTolerance = distance > 50 ? CONSERVATIVE_SIMPLIFY_TOLERANCE * 1.5 : CONSERVATIVE_SIMPLIFY_TOLERANCE;
                finalGeometry = SimplifyGeometry(finalGeometry, simplifyTolerance) ?? finalGeometry;
                if (finalGeometry.IsEmpty || finalGeometry.NumPoints < 2)
                {
                    finalGeometry = trailGeometry;
                    if (finalGeometry.IsEmpty || finalGeometry.NumPoints < 2) return (null, false);
                }
            }

            var intersectedRegions = GetIntersectedRegions(finalGeometry);
            if (intersectedRegions.Count == 0 && _regionsCache.Count != 0)
            {
                return (null, false);
            }

            var (startLoc, endLoc) = GetStartEndLocations(finalGeometry);

            var trailData = new Trail
            {
                OsmId = relation.Id,
                Name = name ?? string.Empty,
                Description = tags.GetValueOrDefault("description", string.Empty),
                Coordinates = finalGeometry,
                Distance = distance,
                Difficulty = DetermineDifficulty(tags, distance),
                TrailType = DetermineTrailType(tags),
                Duration = EstimateDuration(distance, tags),
                StartLocation = startLoc,
                EndLocation = endLoc,
                Tags = ExtractTags(tags),
                Category = DetermineCategory(tags.GetValueOrDefault("network")),
                Network = tags.TryGetValue("network", out var network) ? network : null,
                LastUpdated = DateTime.UtcNow,
                TrailRegions = [.. intersectedRegions.Select(r => new TrailRegion { RegionId = r.Id })]
            };

            var existingTrail = await _context.Trails
                .AsSplitQuery()
                .Include(t => t.TrailRegions)
                .FirstOrDefaultAsync(t => t.OsmId == relation.Id, cancellationToken);

            if (existingTrail != null)
            {
                UpdateTrailEntity(existingTrail, trailData);
                return (existingTrail, false);
            }
            else
            {
                return (trailData, true);
            }
        }

        private Geometry? BuildTrailGeometry(OverpassElement relation, IReadOnlyDictionary<long, OverpassElement> waysDict)
        {
            if (relation.Members == null || relation.Members.Count == 0)
            {
                _logger.LogDebug("Relation {RelationId} has no members.", relation.Id); return null;
            }

            var waySegmentsInOrder = new List<(long id, string? role, List<Coordinate> coords)>();
            var wayMembers = relation.Members.Where(m => m.Type == "way" && m.Ref > 0).ToList();

            _logger.LogDebug("Relation {RelationId}: Extracting {WayMemberCount} way members.", relation.Id, wayMembers.Count);
            foreach (var member in wayMembers)
            {
                if (!waysDict.TryGetValue(member.Ref, out var way))
                {
                    _logger.LogTrace("Relation {RelationId}: Way {WayRef} (Role: '{MemberRole}') not found in waysDict.", relation.Id, member.Ref, member.Role); continue;
                }
                if (way.Geometry == null || way.Geometry.Count < 2)
                {
                    _logger.LogTrace("Relation {RelationId}: Way {WayRef} (Role: '{MemberRole}') has insufficient geometry ({WayGeometryCount} points).", relation.Id, member.Ref, member.Role, way.Geometry?.Count ?? 0); continue;
                }

                var segmentCoords = way.Geometry.Select(g => new Coordinate(g.Lon, g.Lat)).ToList();
                var cleanCoords = CleanConsecutiveDuplicates(segmentCoords, DUPLICATE_POINT_CLEANUP_TOLERANCE);

                if (cleanCoords.Count >= 2) waySegmentsInOrder.Add((member.Ref, member.Role, cleanCoords));
                else _logger.LogTrace("Relation {RelationId}: Way {WayRef} (Role: '{MemberRole}') had < 2 points after cleaning.", relation.Id, member.Ref, member.Role);
            }

            if (waySegmentsInOrder.Count == 0)
            {
                _logger.LogWarning("Relation {RelationId}: No valid way segments extracted after cleaning.", relation.Id); return null;
            }

            var orderedPathsCoordinates = BuildPathsAttemptingToRespectOrder(waySegmentsInOrder, relation.Id);
            if (orderedPathsCoordinates.Count == 0)
            {
                _logger.LogWarning("Relation {RelationId}: 'BuildPathsAttemptingToRespectOrder' returned no paths.", relation.Id); return null;
            }

            var validLineStrings = new List<LineString>();
            foreach (var (pathCoords, index) in orderedPathsCoordinates.Select((coords, idx) => (coords, idx)))
            {
                if (pathCoords.Count >= 2)
                {
                    try
                    {
                        var line = _geometryFactory.CreateLineString([.. pathCoords]);
                        if (!line.IsValid)
                        {
                            _logger.LogDebug("Relation {RelationId}, Path {PathIndex}: Created LineString ({PointCount} pts) is invalid. Attempting Buffer(0).", relation.Id, index, pathCoords.Count);
                            var repairedGeom = line.Buffer(0);
                            if (repairedGeom is LineString repairedLine && repairedLine.IsValid && repairedLine.Coordinates.Length >= 2)
                            {
                                validLineStrings.Add(repairedLine);
                                _logger.LogDebug("Relation {RelationId}, Path {PathIndex}: Repaired to valid LineString ({PointCount} pts).", relation.Id, index, repairedLine.Coordinates.Length);
                            }
                            else if (repairedGeom is MultiLineString repairedMultiLine && repairedMultiLine.IsValid)
                            {
                                for (int geomIdx = 0; geomIdx < repairedMultiLine.NumGeometries; geomIdx++)
                                {
                                    if (repairedMultiLine.GetGeometryN(geomIdx) is LineString component && component.Coordinates.Length >= 2)
                                    {
                                        validLineStrings.Add(component);
                                        _logger.LogDebug("Relation {RelationId}, Path {PathIndex}: Repaired to MultiLineString, extracted valid LineString component ({PointCount} pts).", relation.Id, index, component.Coordinates.Length);
                                    }
                                }
                            }
                            else _logger.LogWarning("Relation {RelationId}, Path {PathIndex}: Buffer(0) did not result in a usable LineString. Original points: {PointCount}, Repaired type: {GeometryType}", relation.Id, index, pathCoords.Count, repairedGeom.GeometryType);
                        }
                        else validLineStrings.Add(line);
                    }
                    catch (Exception ex) { _logger.LogError(ex, "Relation {RelationId}, Path {PathIndex}: Exception creating LineString from path with {PointCount} coords.", relation.Id, index, pathCoords.Count); }
                }
                else _logger.LogDebug("Relation {RelationId}, Path {PathIndex}: Path segment too short ({PointCount} pts), skipping LineString creation.", relation.Id, index, pathCoords.Count);
            }

            if (validLineStrings.Count == 0) { _logger.LogWarning("Relation {RelationId}: No valid LineStrings formed after validation and repair attempts.", relation.Id); return null; }

            if (validLineStrings.Count == 1)
            {
                _logger.LogInformation("Relation {RelationId}: Built a single LineString with {PointCount} points.", relation.Id, validLineStrings[0].NumPoints);
                return validLineStrings[0];
            }
            var resultMultiLineString = _geometryFactory.CreateMultiLineString([.. validLineStrings]);
            _logger.LogInformation("Relation {RelationId}: Built a MultiLineString with {ComponentCount} components, total points: {TotalPointCount}.", relation.Id, resultMultiLineString.NumGeometries, resultMultiLineString.NumPoints);
            return resultMultiLineString;
        }

        private List<List<Coordinate>> BuildPathsAttemptingToRespectOrder(
            List<(long id, string? role, List<Coordinate> coords)> waySegmentsInOrder, long relationId)
        {
            var allConstructedPaths = new List<List<Coordinate>>();
            if (waySegmentsInOrder.Count == 0) return allConstructedPaths;

            var primaryRoleFilter = new HashSet<string?> { null, "", "part", "main", "forward" };
            var primarySegments = waySegmentsInOrder.Where(s => primaryRoleFilter.Contains(s.role?.ToLowerInvariant())).ToList();

            if (primarySegments.Count == 0)
            {
                _logger.LogDebug("Relation {RelationId}: No segments with primary roles found. Using all {WaySegmentsInOrderCount} segments for ordered path construction.", relationId, waySegmentsInOrder.Count);
                primarySegments = waySegmentsInOrder;
                if (primarySegments.Count == 0) return allConstructedPaths;
            }
            _logger.LogDebug("Relation {RelationId}: Starting ordered path construction with {PrimarySegmentsCount} primary/fallback segments.", relationId, primarySegments.Count);

            var currentPathCoordinates = new List<Coordinate>(primarySegments[0].coords);
            _logger.LogTrace("Relation {RelationId}: Path init with segment {SegmentId} ({PointCount} pts).", relationId, primarySegments[0].id, primarySegments[0].coords.Count);

            for (int i = 1; i < primarySegments.Count; i++)
            {
                var (nextSegmentId, _, nextSegmentCoords) = primarySegments[i];
                if (nextSegmentCoords == null || nextSegmentCoords.Count == 0) { _logger.LogTrace("Relation {RelationId}: Next segment {NextSegmentId} is null or empty, skipping.", relationId, nextSegmentId); continue; }
                if (currentPathCoordinates.Count == 0)
                {
                    _logger.LogWarning("Relation {RelationId}: Current path became empty before segment {NextSegmentId}. Re-initializing.", relationId, nextSegmentId);
                    currentPathCoordinates.AddRange(nextSegmentCoords);
                    _logger.LogTrace("Relation {RelationId}: Path re-init with segment {NextSegmentId} ({PointCount} pts).", relationId, nextSegmentId, nextSegmentCoords.Count);
                    continue;
                }

                var lastPtCurrent = currentPathCoordinates.Last();
                var firstPtNext = nextSegmentCoords.First();
                var lastPtNext = nextSegmentCoords.Last();
                bool connected = false;

                if (IsClose(lastPtCurrent, firstPtNext, PRIMARY_CONNECTION_TOLERANCE))
                {
                    currentPathCoordinates.AddRange(nextSegmentCoords.Skip(1)); connected = true;
                    _logger.LogTrace("Relation {RelationId}: Appended segment {NextSegmentId} (normal). Path pts: {PointCount}", relationId, nextSegmentId, currentPathCoordinates.Count);
                }
                else if (IsClose(lastPtCurrent, lastPtNext, PRIMARY_CONNECTION_TOLERANCE))
                {
                    var reversedCoords = nextSegmentCoords.ToList(); reversedCoords.Reverse();
                    currentPathCoordinates.AddRange(reversedCoords.Skip(1)); connected = true;
                    _logger.LogTrace("Relation {RelationId}: Appended segment {NextSegmentId} (reversed). Path pts: {PointCount}", relationId, nextSegmentId, currentPathCoordinates.Count);
                }

                if (!connected)
                {
                    double distToStart = HaversineDistance(lastPtCurrent.Y, lastPtCurrent.X, firstPtNext.Y, firstPtNext.X) * 1000;
                    double distToEnd = HaversineDistance(lastPtCurrent.Y, lastPtCurrent.X, lastPtNext.Y, lastPtNext.X) * 1000;
                    _logger.LogDebug("Relation {RelationId}: Break in ordered path. Segment {NextSegmentId} (start: {FirstPtNextX:F6},{FirstPtNextY:F6}, end: {LastPtNextX:F6},{LastPtNextY:F6}) not close to current path end ({LastPtCurrentX:F6},{LastPtCurrentY:F6}). DistToStart: {DistToStart:F1}m, DistToEnd: {DistToEnd:F1}m. Tolerance: ~{ToleranceM:F1}m ({ToleranceDeg})",
                        relationId, nextSegmentId, firstPtNext.X, firstPtNext.Y, lastPtNext.X, lastPtNext.Y, lastPtCurrent.X, lastPtCurrent.Y, distToStart, distToEnd, PRIMARY_CONNECTION_TOLERANCE * 111000, PRIMARY_CONNECTION_TOLERANCE);
                    if (currentPathCoordinates.Count >= 2) allConstructedPaths.Add([.. currentPathCoordinates]);
                    currentPathCoordinates.Clear();
                    currentPathCoordinates.AddRange(nextSegmentCoords);
                    _logger.LogDebug("Relation {RelationId}: New path started with segment {NextSegmentId} ({PointCount} pts).", relationId, nextSegmentId, nextSegmentCoords.Count);
                }
            }
            if (currentPathCoordinates.Count >= 2) allConstructedPaths.Add(currentPathCoordinates);
            _logger.LogInformation("Relation {RelationId}: Ordered path construction resulted in {PathCount} path component(s).", relationId, allConstructedPaths.Count);
            return allConstructedPaths;
        }

        private static List<Coordinate> CleanConsecutiveDuplicates(List<Coordinate> coords, double tolerance)
        {
            if (coords == null || coords.Count < 2) return coords ?? [];
            var cleanCoords = new List<Coordinate> { coords[0] };
            for (int i = 1; i < coords.Count; i++)
            {
                if (!IsClose(coords[i], coords[i - 1], tolerance)) cleanCoords.Add(coords[i]);
            }
            return cleanCoords;
        }

        private Geometry? SimplifyGeometry(Geometry geometry, double tolerance)
        {
            if (geometry == null || geometry.IsEmpty || tolerance <= 0) return geometry;
            try
            {
                if (geometry is LineString line)
                {
                    if (line.NumPoints < 3) return line;
                    var simplifier = new DouglasPeuckerSimplifier(line) { DistanceTolerance = tolerance };
                    var simplified = simplifier.GetResultGeometry() as LineString;
                    if (simplified != null && simplified.Coordinates.Length >= 2)
                    {
                        _logger.LogTrace("Simplified LineString from {OriginalPointCount} to {SimplifiedPointCount} points for relation (geom user data): {GeometryUserData}", line.NumPoints, simplified.NumPoints, line.UserData ?? "N/A");
                        return simplified;
                    }
                    _logger.LogTrace("LineString simplification resulted in < 2 points or null. Original points: {OriginalPointCount} for relation (geom user data): {GeometryUserData}", line.NumPoints, line.UserData ?? "N/A");
                    return line;
                }
                else if (geometry is MultiLineString multiLine)
                {
                    var simplifiedComponents = new List<LineString>();
                    bool anySimplified = false;
                    for (int i = 0; i < multiLine.NumGeometries; i++)
                    {
                        if (multiLine.GetGeometryN(i) is LineString component)
                        {
                            if (component.NumPoints < 3)
                            {
                                if (component.NumPoints >= 2) simplifiedComponents.Add(component);
                                continue;
                            }
                            var simplifier = new DouglasPeuckerSimplifier(component) { DistanceTolerance = tolerance };
                            var simplifiedComponent = simplifier.GetResultGeometry() as LineString;
                            if (simplifiedComponent != null && simplifiedComponent.Coordinates.Length >= 2)
                            {
                                simplifiedComponents.Add(simplifiedComponent);
                                if (simplifiedComponent.NumPoints < component.NumPoints) anySimplified = true;
                            }
                            else if (component.Coordinates.Length >= 2)
                            {
                                simplifiedComponents.Add(component);
                            }
                        }
                    }
                    if (simplifiedComponents.Count != 0)
                    {
                        var newMultiLine = _geometryFactory.CreateMultiLineString([.. simplifiedComponents]);
                        if (anySimplified)
                        {
                            _logger.LogTrace("Simplified MultiLineString from {OriginalComponentCount} components, {OriginalPointCount} total points to {SimplifiedComponentCount} components, {SimplifiedPointCount} total points for relation (geom user data): {GeometryUserData}",
                               multiLine.NumGeometries, multiLine.NumPoints, newMultiLine.NumGeometries, newMultiLine.NumPoints, multiLine.UserData ?? "N/A");
                        }
                        return newMultiLine;
                    }
                    _logger.LogTrace("MultiLineString simplification resulted in no valid components. Original components: {OriginalComponentCount} for relation (geom user data): {GeometryUserData}", multiLine.NumGeometries, multiLine.UserData ?? "N/A");
                    return multiLine;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to simplify geometry type {GeometryType}. Tolerance: {Tolerance}. Returning original.", geometry.GeometryType, tolerance); }
            return geometry;
        }

        private static (string startLocation, string endLocation) GetStartEndLocations(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty) return ("N/A", "N/A");
            Point? startPoint = null; Point? endPoint = null;
            if (geometry is LineString ls)
            {
                if (ls.NumPoints > 0) { startPoint = ls.StartPoint; endPoint = ls.EndPoint; }
            }
            else if (geometry is MultiLineString mls && mls.NumGeometries > 0)
            {
                if (mls.GetGeometryN(0) is LineString firstComponent && firstComponent.NumPoints > 0) startPoint = firstComponent.StartPoint;
                var lastGeoIndex = mls.NumGeometries - 1;
                if (lastGeoIndex >= 0 && mls.GetGeometryN(lastGeoIndex) is LineString lastComponent && lastComponent.NumPoints > 0) endPoint = lastComponent.EndPoint;
            }
            return (startPoint != null ? FormatCoordinate(startPoint) : "N/A", endPoint != null ? FormatCoordinate(endPoint) : "N/A");
        }

        private static double CalculateDistance(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty) return 0.0;
            if (geometry is LineString ls) return CalculateSingleLineStringDistance(ls);
            if (geometry is MultiLineString mls)
            {
                double total = 0;
                for (int i = 0; i < mls.NumGeometries; i++)
                {
                    if (mls.GetGeometryN(i) is LineString component) total += CalculateSingleLineStringDistance(component);
                }
                return Math.Round(total, 2);
            }
            return 0.0;
        }

        private static double CalculateSingleLineStringDistance(LineString lineString)
        {
            var totalDistance = 0.0;
            var coords = lineString.Coordinates;
            if (coords.Length < 2) return 0.0;
            for (int i = 0; i < coords.Length - 1; i++)
            {
                var d = HaversineDistance(coords[i].Y, coords[i].X, coords[i + 1].Y, coords[i + 1].X);
                if (double.IsFinite(d)) totalDistance += d;
            }
            return Math.Round(totalDistance, 2);
        }

        private List<Region> GetIntersectedRegions(Geometry geometry)
        {
            var intersected = new List<Region>();
            if (geometry == null || geometry.IsEmpty || _regionsCache.Count == 0) return intersected;
            foreach (var region in _regionsCache.Values)
            {
                if (region.Boundary == null || !region.Boundary.IsValid || region.Boundary.IsEmpty) continue;
                try
                {
                    if (geometry.Intersects(region.Boundary)) intersected.Add(region);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Error checking intersection: Trail {TrailGeomType}, Region {RegionName} (ID: {RegionId})", geometry.GeometryType, region.Name, region.Id); }
            }
            return intersected;
        }

        private static string FormatCoordinate(Point point)
        {
            return $"{point.Y:F6},{point.X:F6}";
        }

        private static string DetermineDifficulty(Dictionary<string, string> tags, double distance)
        {
            if (tags.TryGetValue("sac_scale", out var sac))
            {
                return sac.ToLowerInvariant() switch
                {
                    "hiking" => "Easy",
                    "mountain_hiking" => "Moderate",
                    "demanding_mountain_hiking" => "Difficult",
                    "alpine_hiking" => "Very Difficult",
                    "demanding_alpine_hiking" => "Expert",
                    "difficult_alpine_hiking" => "Expert",
                    _ => "Moderate"
                };
            }
            if (tags.TryGetValue("mtb:scale", out var mtbScale))
            {
                return mtbScale switch
                {
                    "0" => "Easy",
                    "1" => "Moderate",
                    "2" => "Difficult",
                    "3" => "Very Difficult",
                    "4" or "5" or "6" => "Expert",
                    _ => "Moderate"
                };
            }
            var routeType = tags.GetValueOrDefault("route", "hiking")?.ToLowerInvariant();
            if (routeType == "bicycle" || routeType == "mtb")
            {
                if (distance < 20) return "Easy"; if (distance < 40) return "Moderate"; if (distance < 80) return "Difficult";
                if (distance < 120) return "Very Difficult"; return "Expert";
            }
            if (distance < 5) return "Easy"; if (distance < 10) return "Moderate"; if (distance < 20) return "Difficult";
            if (distance < 30) return "Very Difficult"; return "Expert";
        }

        private static string DetermineTrailType(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("route", out var route))
            {
                return route.ToLowerInvariant() switch
                {
                    "hiking" => "Hiking",
                    "foot" => "Walking",
                    "bicycle" => "Cycling",
                    "mtb" => "Mountain Biking",
                    _ => "Hiking"
                };
            }
            if (tags.TryGetValue("highway", out var highway))
            {
                return highway.ToLowerInvariant() switch
                {
                    "cycleway" => "Cycling",
                    "footway" => "Walking",
                    "path" => "Hiking",
                    "track" => "Hiking",
                    _ => "Hiking"
                };
            }
            return "Hiking";
        }

        private static string DetermineCategory(string? network)
        {
            if (string.IsNullOrEmpty(network)) return "Local";
            var n = network.ToLowerInvariant();
            if (n.Contains("iwn") || n.Contains("icn")) return "International";
            if (n.Contains("nwn") || n.Contains("ncn")) return "National";
            if (n.Contains("rwn") || n.Contains("rcn")) return "Regional";
            if (n.Contains("lwn") || n.Contains("lcn")) return "Local";
            return "Local";
        }

        private static double EstimateDuration(double distance, Dictionary<string, string> tags)
        {
            if (!double.IsFinite(distance) || distance <= 0) return 0.5;
            string trailType = DetermineTrailType(tags);

            double speedKmh = trailType switch
            {
                "Cycling" => 15.0,
                "Mountain Biking" => 10.0,
                "Walking" => 3.0,
                "Hiking" => 3.5,
                _ => 3.5
            };

            if (tags.TryGetValue("sac_scale", out var sac) && (trailType == "Hiking" || trailType == "Walking"))
            {
                var multiplier = sac.ToLowerInvariant() switch
                {
                    "hiking" => 1.0,
                    "mountain_hiking" => 0.85,
                    "demanding_mountain_hiking" => 0.7,
                    "alpine_hiking" => 0.6,
                    "demanding_alpine_hiking" => 0.5,
                    "difficult_alpine_hiking" => 0.4,
                    _ => 0.9
                };
                speedKmh *= multiplier;
            }
            if (speedKmh <= 0.1) speedKmh = 0.1;
            var hours = distance / speedKmh;
            hours *= (distance < 10 ? 1.1 : 1.25);
            return Math.Max(0.25, Math.Min(Math.Round(hours * 2) / 2, 48.0));
        }

        private static List<string> ExtractTags(Dictionary<string, string> tags)
        {
            var relevantKeys = new HashSet<string> { "name", "ref", "network", "route", "operator", "sac_scale", "surface", "smoothness", "mtb:scale", "highway", "tourism", "historic", "marking", "symbol", "osmc:symbol" };
            return [.. tags.Where(t => relevantKeys.Contains(t.Key) && !string.IsNullOrWhiteSpace(t.Value)).Select(t => $"{t.Key}={t.Value}")];
        }

        private static bool IsClose(Coordinate c1, Coordinate c2, double tolerance)
        {
            return Math.Abs(c1.X - c2.X) < tolerance && Math.Abs(c1.Y - c2.Y) < tolerance;
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var rLat1 = ToRadians(lat1);
            var rLat2 = ToRadians(lat2);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(rLat1) * Math.Cos(rLat2);
            if (a < 0) a = 0;
            if (a > 1) a = 1;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);

        private void UpdateTrailEntity(Trail existing, Trail newTrailData)
        {
            existing.Name = newTrailData.Name;
            existing.Description = newTrailData.Description;
            existing.Coordinates = newTrailData.Coordinates;
            existing.Distance = newTrailData.Distance;
            existing.Difficulty = newTrailData.Difficulty;
            existing.TrailType = newTrailData.TrailType;
            existing.Duration = newTrailData.Duration;
            existing.StartLocation = newTrailData.StartLocation;
            existing.EndLocation = newTrailData.EndLocation;
            existing.Tags = [.. newTrailData.Tags ?? []];
            existing.Category = newTrailData.Category;
            existing.Network = newTrailData.Network;
            existing.LastUpdated = DateTime.UtcNow;

            var newRegionIds = newTrailData.TrailRegions.Select(tr => tr.RegionId).ToHashSet();
            var existingRegionIds = existing.TrailRegions.Select(tr => tr.RegionId).ToHashSet();
            var regionIdsToAdd = newRegionIds.Except(existingRegionIds).ToList();
            var trailRegionsToRemove = existing.TrailRegions.Where(tr => !newRegionIds.Contains(tr.RegionId)).ToList();

            foreach (var trToRemove in trailRegionsToRemove) { existing.TrailRegions.Remove(trToRemove); }
            foreach (var regionIdToAdd in regionIdsToAdd) { existing.TrailRegions.Add(new TrailRegion { RegionId = regionIdToAdd }); }
            _logger.LogDebug("Updated TrailRegions for Trail OSM ID {OsmId}. Added: {RegionsToAddCount}, Removed: {RegionsToRemoveCount}", existing.OsmId, regionIdsToAdd.Count, trailRegionsToRemove.Count);
        }
    }
}