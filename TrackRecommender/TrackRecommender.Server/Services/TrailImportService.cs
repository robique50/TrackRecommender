using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using System.Text.Json;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TrailImportService> _logger;
        private readonly GeometryFactory _geometryFactory;
        private readonly RegionImportService _regionImportService;

        private int _newTrailCounter = 0;
        private int _updatedTrailCounter = 0;

        private const int OVERPASS_TIMEOUT_SECONDS = 180;
        private const int SAVE_CHANGES_BATCH_SIZE = 100;
        private const double MAX_VALID_DISTANCE_KM = 1000;
        private const double MIN_VALID_DISTANCE_KM = 0.1;
        private const double GEOMETRY_SIMPLIFY_TOLERANCE = 0.0001;

        private Dictionary<int, Region> _romanianRegionsCache = new Dictionary<int, Region>();

        public TrailImportService(
            AppDbContext context,
            HttpClient httpClient,
            ILogger<TrailImportService> logger,
            RegionImportService regionImportService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _regionImportService = regionImportService ?? throw new ArgumentNullException(nameof(regionImportService));
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _httpClient.Timeout = TimeSpan.FromSeconds(OVERPASS_TIMEOUT_SECONDS + 120);
        }

        public async Task ImportAllTrailsAsync()
        {
            _logger.LogInformation("Starting optimized trail import process...");
            _newTrailCounter = 0;
            _updatedTrailCounter = 0;

            try
            {
                await EnsureRegionsExist();

                await LoadRomanianRegionsCacheAsync();

                if (!_romanianRegionsCache.Any())
                {
                    _logger.LogWarning("No Romanian regions found.");
                    return;
                }

                var overpassResponse = await FetchTrailsFromOverpass();
                if (overpassResponse?.Elements == null || !overpassResponse.Elements.Any())
                {
                    _logger.LogInformation("No trails returned from Overpass API");
                    return;
                }

                await ProcessTrailsResponse(overpassResponse);

                _logger.LogInformation($"Import completed. New: {_newTrailCounter}, Updated: {_updatedTrailCounter}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during trail import");
                throw;
            }
        }

        private async Task EnsureRegionsExist()
        {
            var regionCount = await _context.Regions.CountAsync();
            if (regionCount == 0)
            {
                _logger.LogInformation("No regions found. Importing Romanian counties...");
                await _regionImportService.ImportRomanianCountiesAsync();
            }
        }

        private async Task<OverpassResponse?> FetchTrailsFromOverpass()
        {
            string query = $@"
                [out:json][timeout:{OVERPASS_TIMEOUT_SECONDS}];
                area[""name""=""România""][""admin_level""=""2""]->.searchArea;
                (
                    relation(area.searchArea)[""type""=""route""][""route""~""^(hiking|foot|bicycle|mtb|running|trail_running)$""][""name""];
                    relation(area.searchArea)[""type""=""route""][""network""~""^(iwn|nwn|rwn|lwn|icn|ncn|rcn|lcn)$""][""route""~""^(hiking|foot|bicycle|mtb|running|trail_running)$""][""name""];
                );
                out body;
                >;
                out skel qt geom;";

            return await ExecuteOverpassQueryAsync(query);
        }

        private async Task ProcessTrailsResponse(OverpassResponse response)
        {
            var relations = response.Elements
                .Where(e => e.Type == "relation" && e.Tags?.ContainsKey("name") == true)
                .ToList();

            var waysDict = response.Elements
                .Where(e => e.Type == "way")
                .ToDictionary(w => w.Id, w => w);

            _logger.LogInformation($"Processing {relations.Count} trail relations...");

            var trailsToAddBatch = new List<Trail>();
            int processedCount = 0;

            foreach (var relation in relations)
            {
                processedCount++;

                if (processedCount % 10 == 0)
                {
                    _logger.LogInformation($"Processing relation {processedCount}/{relations.Count}...");
                }

                var processedData = await CreateTrailDataFromRelation(relation, waysDict);
                if (processedData == null) continue;

                await ProcessSingleTrail(processedData, relation.Id, trailsToAddBatch);

                if (trailsToAddBatch.Count >= SAVE_CHANGES_BATCH_SIZE || processedCount == relations.Count)
                {
                    await SaveTrailsBatch(trailsToAddBatch);
                    trailsToAddBatch.Clear();
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await SaveChangesToDbAsync();
            }
        }

        private async Task ProcessSingleTrail(ProcessedTrailDto processedData, long osmId, List<Trail> batch)
        {
            var existingTrail = await _context.Trails
                .Include(t => t.TrailRegions)
                .FirstOrDefaultAsync(t => t.OsmId == osmId);

            if (existingTrail != null)
            {
                UpdateExistingTrailWithProcessedData(existingTrail, processedData);
                _updatedTrailCounter++;
            }
            else
            {
                var newTrail = ConvertProcessedDataToTrailEntity(processedData, osmId);
                batch.Add(newTrail);
                _newTrailCounter++;
            }
        }

        private async Task SaveTrailsBatch(List<Trail> trails)
        {
            if (trails.Any())
            {
                await _context.Trails.AddRangeAsync(trails);
            }

            if (trails.Any() || _context.ChangeTracker.HasChanges())
            {
                await SaveChangesToDbAsync();
            }
        }

        private async Task LoadRomanianRegionsCacheAsync()
        {
            var regions = await _context.Regions
                .Where(r => r.Boundary != null && r.Boundary.IsValid)
                .AsNoTracking()
                .ToListAsync();

            _romanianRegionsCache = regions.ToDictionary(r => r.Id, r => r);
            _logger.LogInformation($"Loaded {_romanianRegionsCache.Count} valid regions in cache");
        }

        private async Task SaveChangesToDbAsync()
        {
            try
            {
                var changes = await _context.SaveChangesAsync();
                _logger.LogInformation($"Saved {changes} changes to database");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving changes to database");

                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }
                throw;
            }
        }

        private async Task<ProcessedTrailDto?> CreateTrailDataFromRelation(
            OverpassElement relation,
            Dictionary<long, OverpassElement> waysDict)
        {
            string? trailName = relation.Tags?.GetValueOrDefault("name");
            if (string.IsNullOrWhiteSpace(trailName)) return null;

            if (IsNonRomanianTrailByName(trailName, relation.Tags ?? new Dictionary<string, string>()))
            {
                _logger.LogDebug($"Skipping non-Romanian trail: {trailName}");
                return null;
            }

            var assembledCoordinates = BuildTrailCoordinatesFromRelationMembers(relation, waysDict, trailName);
            if (assembledCoordinates == null || assembledCoordinates.Count < 2)
            {
                _logger.LogWarning($"Could not build valid coordinates for trail: {trailName}");
                return null;
            }

            LineString rawLineString;
            try
            {
                rawLineString = _geometryFactory.CreateLineString(assembledCoordinates.ToArray());
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, $"Failed to create LineString for trail: {trailName}");
                return null;
            }

            if (!rawLineString.IsValid)
            {
                _logger.LogWarning($"Invalid geometry for trail: {trailName}, attempting repair...");
                var repaired = rawLineString.Buffer(0);
                if (repaired is LineString rl && rl.IsValid && rl.Coordinates.Length >= 2)
                {
                    rawLineString = rl;
                    _logger.LogInformation($"Successfully repaired geometry for trail: {trailName}");
                }
                else
                {
                    _logger.LogError($"Could not repair geometry for trail: {trailName}");
                    return null;
                }
            }

            if (!IsTrailSpatiallyInRomania(rawLineString) && _romanianRegionsCache.Any())
            {
                _logger.LogDebug($"Trail not in Romania: {trailName}");
                return null;
            }

            var simplifiedLineString = SimplifyTrailGeometry(rawLineString, trailName);
            if (simplifiedLineString.Coordinates.Length < 2) return null;

            double distanceKm = CalculateHaversineDistanceForLineString(simplifiedLineString);
            if (distanceKm < MIN_VALID_DISTANCE_KM || distanceKm > MAX_VALID_DISTANCE_KM)
            {
                _logger.LogWarning($"Trail {trailName} has invalid distance: {distanceKm}km");
                return null;
            }

            var associatedRegions = DetermineIntersectedRomanianRegions(simplifiedLineString);
            var osmTags = relation.Tags ?? new Dictionary<string, string>();

            return new ProcessedTrailDto
            {
                Name = trailName,
                Description = osmTags.GetValueOrDefault("description"),
                Coordinates = simplifiedLineString,
                DistanceKm = distanceKm,
                Difficulty = DetermineDifficulty(osmTags, distanceKm),
                TrailType = DetermineTrailTypeFromOsm(osmTags),
                EstimatedDurationHours = EstimateDuration(distanceKm, osmTags.GetValueOrDefault("sac_scale") ?? "Easy", DetermineTrailTypeFromOsm(osmTags), osmTags),
                StartLocation = $"{simplifiedLineString.StartPoint.Y:F6},{simplifiedLineString.StartPoint.X:F6}",
                EndLocation = $"{simplifiedLineString.EndPoint.Y:F6},{simplifiedLineString.EndPoint.X:F6}",
                Tags = ExtractRelevantTags(osmTags),
                Category = DetermineCategoryFromNetwork(osmTags.GetValueOrDefault("network")),
                Network = osmTags.GetValueOrDefault("network"),
                AssociatedRegions = associatedRegions,
                OsmTags = osmTags
            };
        }

        private LineString SimplifyTrailGeometry(LineString rawLineString, string trailName)
        {
            if (rawLineString.Coordinates.Length <= 20)
            {
                return rawLineString;
            }

            try
            {
                var simplifier = new DouglasPeuckerSimplifier(rawLineString)
                {
                    DistanceTolerance = GEOMETRY_SIMPLIFY_TOLERANCE
                };

                var simplifiedResult = simplifier.GetResultGeometry();
                if (simplifiedResult is LineString simplifiedLs && simplifiedLs.Coordinates.Length >= 2)
                {
                    _logger.LogDebug($"Simplified trail {trailName} from {rawLineString.Coordinates.Length} to {simplifiedLs.Coordinates.Length} points");
                    return simplifiedLs;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not simplify geometry for trail: {trailName}");
            }

            return rawLineString;
        }

        private Trail ConvertProcessedDataToTrailEntity(ProcessedTrailDto dto, long osmId)
        {
            var trail = new Trail
            {
                OsmId = osmId,
                Name = dto.Name,
                Description = dto.Description ?? string.Empty,
                Coordinates = dto.Coordinates,
                Distance = dto.DistanceKm,
                Difficulty = dto.Difficulty,
                TrailType = dto.TrailType,
                Duration = dto.EstimatedDurationHours,
                StartLocation = dto.StartLocation,
                EndLocation = dto.EndLocation,
                Tags = dto.Tags,
                Category = dto.Category,
                Network = dto.Network,
                LastUpdated = DateTime.UtcNow
            };
            if (dto.AssociatedRegions.Any())
            {
                trail.TrailRegions = dto.AssociatedRegions
                    .Select(r => new TrailRegion { RegionId = r.Id })
                    .ToList();
            }
            return trail;
        }

        private void UpdateExistingTrailWithProcessedData(Trail existingTrail, ProcessedTrailDto dto)
        {
            existingTrail.Name = dto.Name;
            existingTrail.Description = dto.Description ?? string.Empty;
            existingTrail.Coordinates = dto.Coordinates;
            existingTrail.Distance = dto.DistanceKm;
            existingTrail.Difficulty = dto.Difficulty;
            existingTrail.TrailType = dto.TrailType;
            existingTrail.Duration = dto.EstimatedDurationHours;
            existingTrail.StartLocation = dto.StartLocation;
            existingTrail.EndLocation = dto.EndLocation;
            existingTrail.Tags = dto.Tags;
            existingTrail.Category = dto.Category;
            existingTrail.Network = dto.Network;
            existingTrail.LastUpdated = DateTime.UtcNow;

            var newRegionIds = dto.AssociatedRegions.Select(r => r.Id).ToHashSet();
            var currentRegionIds = existingTrail.TrailRegions.Select(tr => tr.RegionId).ToHashSet();

            var regionsToRemove = existingTrail.TrailRegions.Where(tr => !newRegionIds.Contains(tr.RegionId)).ToList();
            foreach (var trToRemove in regionsToRemove)
            {
                existingTrail.TrailRegions.Remove(trToRemove);
            }

            foreach (var regionIdToAdd in newRegionIds)
            {
                if (!currentRegionIds.Contains(regionIdToAdd) && _romanianRegionsCache.ContainsKey(regionIdToAdd))
                {
                    existingTrail.TrailRegions.Add(new TrailRegion { RegionId = regionIdToAdd });
                }
            }
            _context.Entry(existingTrail).State = EntityState.Modified;
        }

        private List<Coordinate>? BuildTrailCoordinatesFromRelationMembers(
            OverpassElement relation,
            Dictionary<long, OverpassElement> waysDict,
            string trailNameForLogging)
        {
            if (relation.Members == null || !relation.Members.Any()) return null;

            var waySegments = new List<(long id, List<Coordinate> coords, long firstNodeId, long lastNodeId, string role)>();
            foreach (var member in relation.Members)
            {
                if (member.Type != "way" || !waysDict.TryGetValue(member.Ref, out var wayElement) ||
                    wayElement.Nodes == null || wayElement.Nodes.Count < 2 ||
                    wayElement.Geometry == null || wayElement.Geometry.Count < 2)
                {
                    continue;
                }
                var wayCoords = wayElement.Geometry.Select(g => new Coordinate(g.Lon, g.Lat)).ToList();
                waySegments.Add((member.Ref, wayCoords ?? new List<Coordinate>(), wayElement.Nodes.First(), wayElement.Nodes.Last(), member.Role ?? ""));
            }

            if (!waySegments.Any()) return null;

            var orderedPathCoordinates = new List<Coordinate>();
            var remainingSegments = new List<(long id, List<Coordinate> coords, long firstNodeId, long lastNodeId, string role)>(waySegments);

            if (!remainingSegments.Any()) return orderedPathCoordinates;

            (long id, List<Coordinate> coords, long firstNodeId, long lastNodeId, string role) startSegment;
            var preferredStart = remainingSegments.FirstOrDefault(s => string.IsNullOrEmpty(s.role) || s.role.Equals("forward", StringComparison.OrdinalIgnoreCase));

            if (preferredStart.coords != null)
            {
                startSegment = preferredStart;
            }
            else if (remainingSegments.Any())
            {
                startSegment = remainingSegments.First();
            }
            else
            {
                return null;
            }
            if (startSegment.coords == null) return null;


            List<Coordinate> currentCoords = new List<Coordinate>(startSegment.coords);
            long currentPathEndNodeId = startSegment.lastNodeId;

            if (startSegment.role.Equals("backward", StringComparison.OrdinalIgnoreCase))
            {
                currentCoords.Reverse();
                currentPathEndNodeId = startSegment.firstNodeId;
            }

            orderedPathCoordinates.AddRange(currentCoords);
            remainingSegments.Remove(startSegment);

            int iterations = 0;
            while (remainingSegments.Any() && iterations < waySegments.Count * 2)
            {
                iterations++;
                bool foundNext = false;
                (long id, List<Coordinate> coords, long firstNodeId, long lastNodeId, string role) bestCandidate = default;
                bool reverseCandidate = false;

                foreach (var candidate in remainingSegments)
                {
                    if (candidate.coords == null) continue;
                    if (candidate.firstNodeId == currentPathEndNodeId)
                    {
                        bestCandidate = candidate;
                        reverseCandidate = false;
                        foundNext = true;
                        break;
                    }
                    if (candidate.lastNodeId == currentPathEndNodeId)
                    {
                        bestCandidate = candidate;
                        reverseCandidate = true;
                        foundNext = true;
                        break;
                    }
                }

                if (foundNext && bestCandidate.coords != null)
                {
                    var segmentToAddCoords = new List<Coordinate>(bestCandidate.coords);
                    if (reverseCandidate)
                    {
                        segmentToAddCoords.Reverse();
                        currentPathEndNodeId = bestCandidate.firstNodeId;
                    }
                    else
                    {
                        currentPathEndNodeId = bestCandidate.lastNodeId;
                    }

                    if (orderedPathCoordinates.Any() && segmentToAddCoords.Any() &&
                        orderedPathCoordinates.Last().Equals2D(segmentToAddCoords.First(), 0.000001))
                    {
                        segmentToAddCoords.RemoveAt(0);
                    }

                    if (segmentToAddCoords.Any()) orderedPathCoordinates.AddRange(segmentToAddCoords);
                    remainingSegments.Remove(bestCandidate);
                }
                else
                {
                    break;
                }
            }

            return orderedPathCoordinates.Count >= 2 ? orderedPathCoordinates : null;
        }

        private bool IsNonRomanianTrailByName(string name, Dictionary<string, string> tags)
        {
            var nameLower = name.ToLowerInvariant();
            var nonRomanianKeywords = new[] { "ukraine", "ukraina", "україна", "hungary", "magyarország", "serbia", "србија", "bulgaria", "българия" };
            if (nonRomanianKeywords.Any(keyword => nameLower.Contains(keyword))) return true;
            foreach (var keyword in nonRomanianKeywords)
            {
                if (tags.Any(tag => tag.Key.StartsWith("name:") && tag.Value.ToLowerInvariant().Contains(keyword))) return true;
            }
            return false;
        }

        private bool IsTrailSpatiallyInRomania(LineString trailGeometry)
        {
            if (trailGeometry == null || !trailGeometry.IsValid || trailGeometry.IsEmpty) return false;
            if (!_romanianRegionsCache.Any())
            {
                var centroid = trailGeometry.Centroid;
                if (centroid == null || !centroid.IsValid) return false;
                const double MinLat = 43.5, MaxLat = 48.3; const double MinLon = 20.0, MaxLon = 29.8;
                return centroid.Y >= MinLat && centroid.Y <= MaxLat && centroid.X >= MinLon && centroid.X <= MaxLon;
            }
            foreach (var region in _romanianRegionsCache.Values)
            {
                if (region.Boundary != null && region.Boundary.IsValid && trailGeometry.Intersects(region.Boundary)) return true;
            }
            return false;
        }

        private List<Region> DetermineIntersectedRomanianRegions(LineString trailGeometry)
        {
            var intersectedRegions = new HashSet<Region>(new RegionIdComparer());
            if (trailGeometry == null || !trailGeometry.IsValid || trailGeometry.IsEmpty || !_romanianRegionsCache.Any())
                return new List<Region>();

            foreach (var region in _romanianRegionsCache.Values)
            {
                if (region.Boundary != null && region.Boundary.IsValid && !region.Boundary.IsEmpty)
                {
                    try { if (trailGeometry.Intersects(region.Boundary)) intersectedRegions.Add(region); }
                    catch { }
                }
            }
            return intersectedRegions.ToList();
        }

        private class RegionIdComparer : IEqualityComparer<Region>
        {
            public bool Equals(Region? x, Region? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.Id == y.Id;
            }
            public int GetHashCode(Region? obj)
            {
                return obj is null ? 0 : obj.Id.GetHashCode();
            }
        }

        private double CalculateHaversineDistanceForLineString(LineString line)
        {
            double totalDistance = 0;
            var coordinates = line.Coordinates;
            if (coordinates == null || coordinates.Length < 2) return 0;
            for (int i = 0; i < coordinates.Length - 1; i++)
            {
                totalDistance += CalculateHaversineDistance(coordinates[i].Y, coordinates[i].X, coordinates[i + 1].Y, coordinates[i + 1].X);
            }
            return Math.Round(totalDistance, 2);
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * (Math.PI / 180.0);

        private string DetermineCategoryFromNetwork(string? network)
        {
            if (string.IsNullOrEmpty(network))
                return "Local";

            var n = network.ToLowerInvariant();

            if (n.Contains("iwn") || n.Contains("icn"))
                return "International";
            if (n.Contains("nwn") || n.Contains("ncn"))
                return "National";
            if (n.Contains("rwn") || n.Contains("rcn"))
                return "Regional";

            return "Local";
        }

        private string DetermineDifficulty(Dictionary<string, string> tags, double distance)
        {
            if (tags.TryGetValue("sac_scale", out var s))
                return s;
            if (tags.TryGetValue("difficulty", out var d))
                return d;

            if (distance < 5)
                return "Easy";
            if (distance < 15)
                return "Moderate";

            return "Difficult";
        }

        private string DetermineTrailTypeFromOsm(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("route", out var r))
                return r;

            return "Hiking";
        }

        private List<string> ExtractRelevantTags(Dictionary<string, string> tags)
        {
            var k = new[] { "name", "ref", "network", "route", "operator", "sac_scale", "surface" };
            var relevantTags = tags
                .Where(kvp => k.Contains(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{kvp.Key}={kvp.Value}")
                .ToList();

            if (!tags.ContainsKey("source"))
                relevantTags.Add("source=OpenStreetMap");

            return relevantTags;
        }

        private double EstimateDuration(double distance, string difficulty, string trailType, Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("duration", out var ds) && TryParseOsmDuration(ds, out var oh))
                return oh;

            var spd = 3.5;

            if (trailType.Contains("bike", StringComparison.OrdinalIgnoreCase))
                spd = 12;

            if (difficulty.Contains("easy", StringComparison.OrdinalIgnoreCase))
                spd *= 1.2;
            if (difficulty.Contains("difficult", StringComparison.OrdinalIgnoreCase))
                spd *= 0.8;

            return Math.Round(distance / Math.Max(1, spd) * 1.2, 1);
        }

        private bool TryParseOsmDuration(string durationStr, out double hours)
        {
            hours = 0;

            if (string.IsNullOrWhiteSpace(durationStr))
                return false;

            if (durationStr.StartsWith("PT"))
            {
                try
                {
                    hours = System.Xml.XmlConvert.ToTimeSpan(durationStr).TotalHours;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            var p = durationStr.Split(':');
            if (p.Length >= 2 && int.TryParse(p[0], out int h) && int.TryParse(p[1], out int m))
            {
                hours = h + m / 60.0;
                return true;
            }

            if (double.TryParse(durationStr.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double dh))
            {
                hours = dh;
                return true;
            }

            return false;
        }


        private async Task<OverpassResponse?> ExecuteOverpassQueryAsync(string overpassQuery)
        {
            int maxRetries = 3; int currentRetry = 0; TimeSpan retryDelay = TimeSpan.FromSeconds(15);
            while (currentRetry <= maxRetries)
            {
                try
                {
                    var content = new StringContent($"data={Uri.EscapeDataString(overpassQuery)}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrWhiteSpace(json)) return null;
                        return JsonSerializer.Deserialize<OverpassResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    {
                        currentRetry++; if (currentRetry <= maxRetries) { await Task.Delay(retryDelay); retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 1.5 + Random.Shared.Next(1, 5)); continue; }
                    }
                    return null;
                }
                catch (TaskCanceledException)
                {
                    currentRetry++; if (currentRetry <= maxRetries) { await Task.Delay(retryDelay); retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 1.5 + Random.Shared.Next(1, 5)); continue; }
                    return null;
                }
                catch { return null; }
            }
            return null;
        }
    }
}