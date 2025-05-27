using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Text.Json;
using TrackRecommender.Server.Models;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class RegionImportService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<RegionImportService> _logger;
        private readonly GeometryFactory _geometryFactory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const int OVERPASS_TIMEOUT_SECONDS = 180;
        private const string OVERPASS_API_URL = "https://overpass-api.de/api/interpreter";

        public RegionImportService(
            AppDbContext context,
            HttpClient httpClient,
            ILogger<RegionImportService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _httpClient.Timeout = TimeSpan.FromSeconds(OVERPASS_TIMEOUT_SECONDS + 30);
        }

        public async Task<List<Region>> ImportRomanianCountiesAsync()
        {
            var importedRegions = new List<Region>();

            try
            {
                string query = $@"
            [out:json][timeout:{OVERPASS_TIMEOUT_SECONDS}];
            (
              relation[""ISO3166-2""~""^RO-""][""admin_level""=""4""][""boundary""=""administrative""];
              relation[""place""=""county""][""is_in:country""=""Romania""];
              relation[""admin_level""=""4""][""boundary""=""administrative""][""is_in:country""=""Romania""];
            );
            (._;>;);
            out geom;";

                _logger.LogInformation($"Executing Overpass query: {query}");
                var overpassResponse = await ExecuteOverpassQueryAsync(query);

                if (overpassResponse?.Elements == null || overpassResponse.Elements.Count == 0)
                {
                    _logger.LogWarning("No counties returned from Overpass API");
                    return importedRegions;
                }

                var relations = overpassResponse.Elements.Where(e => e.Type == "relation").ToList();
                var ways = overpassResponse.Elements.Where(e => e.Type == "way").ToDictionary(w => w.Id, w => w);
                var nodes = overpassResponse.Elements.Where(e => e.Type == "node").ToDictionary(n => n.Id, n => n);

                _logger.LogInformation($"Found {relations.Count} potential counties to process");
                _logger.LogInformation($"Found {ways.Count} ways and {nodes.Count} nodes");

                foreach (var rel in relations.Take(5))
                {
                    var name = rel.Tags?.GetValueOrDefault("name", "Unknown");
                    var adminLevel = rel.Tags?.GetValueOrDefault("admin_level", "Unknown");
                    _logger.LogInformation($"Found relation: ID={rel.Id}, Name={name}, AdminLevel={adminLevel}");
                }

                foreach (var element in relations)
                {
                    try
                    {
                        var region = await ProcessCountyElement(element, ways, nodes);
                        if (region != null)
                        {
                            importedRegions.Add(region);
                            _logger.LogInformation($"Successfully processed county: {region.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        var name = element.Tags?.GetValueOrDefault("name", "Unknown");
                        _logger.LogError(ex, $"Error processing county '{name}' with ID {element.Id}");
                    }
                }

                if (importedRegions.Count != 0)
                {
                    await SaveRegionsToDatabase(importedRegions);
                    _logger.LogInformation($"Successfully imported {importedRegions.Count} counties");
                }
                else
                {
                    _logger.LogWarning("No counties were successfully processed");
                }

                return importedRegions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during counties import");
                throw;
            }
        }

        private async Task<Region?> ProcessCountyElement(
            OverpassElement element,
            Dictionary<long, OverpassElement> ways,
            Dictionary<long, OverpassElement> nodes)
        {
            var tags = element.Tags;
            if (tags == null)
            {
                _logger.LogWarning($"Element {element.Id} (type: {element.Type}) has no tags, skipping.");
                return null;
            }

            string? countyName = tags.GetValueOrDefault("name:ro") ??
                                 tags.GetValueOrDefault("name") ??
                                 tags.GetValueOrDefault("official_name");

            if (string.IsNullOrWhiteSpace(countyName))
            {
                _logger.LogWarning($"County element {element.Id} has no recognizable name tag (name:ro, name, official_name). " +
                    $"Found tags: {string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}"))}");
                return null;
            }

            var adminLevel = tags.GetValueOrDefault("admin_level", "");
            var boundaryType = tags.GetValueOrDefault("boundary", "");

            if (adminLevel != "4" || boundaryType != "administrative")
            {
                _logger.LogInformation($"Skipping element '{countyName}' (ID: {element.Id}) - not a valid county (expected admin_level=4, " +
                    $"got '{adminLevel}'; expected boundary=administrative, got '{boundaryType}')");
                return null;
            }

            _logger.LogInformation($"Processing county: {countyName} (ID: {element.Id}), " +
                $"AdminLevel: {adminLevel}, Boundary: {boundaryType}");

            var boundaryGeometry = BuildCountyBoundary(element, ways, nodes);

            if (boundaryGeometry == null)
            {
                _logger.LogWarning($"Could not build boundary for county {countyName} (ID: {element.Id}).");
                return null;
            }

            _logger.LogInformation($"Successfully geometry built for {countyName} (ID: {element.Id}). " +
                $"Type: {boundaryGeometry.GeometryType}, IsValid: " +
                $"{boundaryGeometry.IsValid}, IsEmpty: {boundaryGeometry.IsEmpty}.");

            if (!boundaryGeometry.IsValid)
            {
                _logger.LogWarning($"Geometry for {countyName} " +
                    $"(ID: {element.Id}) is initially Invalid. WKT (original): {boundaryGeometry.AsText()}");
            }

            boundaryGeometry.SRID = _geometryFactory.SRID;

            var existingRegion = await _context.Regions
                .FirstOrDefaultAsync(r => r.Name == countyName);

            if (existingRegion != null)
            {
                _logger.LogInformation($"County {countyName} (ID: {existingRegion.Id}) already exists. Updating boundary. New geometry IsValid: {boundaryGeometry.IsValid}");
                existingRegion.Boundary = boundaryGeometry;
                return existingRegion;
            }
            else
            {
                _logger.LogInformation($"Creating new region entry for {countyName}. Geometry IsValid: {boundaryGeometry.IsValid}");
                var newRegion = new Region
                {
                    Name = countyName,
                    Boundary = boundaryGeometry
                };
                return newRegion;
            }
        }

        private Polygon? BuildCountyBoundary(
            OverpassElement element,
            Dictionary<long, OverpassElement> ways,
            Dictionary<long, OverpassElement> nodes)
        {
            if (element.Members == null || element.Members.Count == 0)
            {
                _logger.LogWarning($"No members found for element {element.Id}");
                return null;
            }

            _logger.LogInformation($"Building boundary for element {element.Id} with {element.Members.Count} members");

            var outerWays = new List<(long id, List<Coordinate> coords)>();
            var innerWays = new List<(long id, List<Coordinate> coords)>();

            foreach (var member in element.Members.Where(m => m.Type == "way"))
            {
                if (!ways.TryGetValue(member.Ref, out var way))
                {
                    _logger.LogDebug($"Way {member.Ref} not found in ways dictionary");
                    continue;
                }

                List<Coordinate> wayCoords = [];

                if (way.Geometry != null && way.Geometry.Count >= 2)
                {
                    wayCoords = [.. way.Geometry.Select(g => new Coordinate(g.Lon, g.Lat))];
                }
                else if (way.Nodes != null && way.Nodes.Count >= 2)
                {
                    foreach (var nodeId in way.Nodes)
                    {
                        if (nodes.TryGetValue(nodeId, out var node) && node.Lat.HasValue && node.Lon.HasValue)
                        {
                            wayCoords.Add(new Coordinate(node.Lon.Value, node.Lat.Value));
                        }
                    }
                }

                if (wayCoords.Count < 2)
                {
                    _logger.LogDebug($"Way {member.Ref} has insufficient coordinates");
                    continue;
                }

                if (member.Role == "outer" || string.IsNullOrEmpty(member.Role))
                {
                    outerWays.Add((member.Ref, wayCoords));
                }
                else if (member.Role == "inner")
                {
                    innerWays.Add((member.Ref, wayCoords));
                }
            }

            if (outerWays.Count == 0)
            {
                _logger.LogWarning($"No valid outer ways found for element {element.Id}");
                return null;
            }

            var outerRing = BuildRingFromWays(outerWays, element.Id);
            if (outerRing == null)
            {
                _logger.LogWarning($"Could not build outer ring for element {element.Id}");
                return null;
            }

            var innerRings = new List<LinearRing>();
            if (innerWays.Count != 0)
            {
                foreach (var innerWayGroup in GroupConnectedWays(innerWays))
                {
                    var innerRing = BuildRingFromWays(innerWayGroup, element.Id);
                    if (innerRing != null)
                    {
                        innerRings.Add(innerRing);
                    }
                }
            }

            try
            {
                Polygon polygon;
                if (innerRings.Count != 0)
                {
                    polygon = _geometryFactory.CreatePolygon(outerRing, [.. innerRings]);
                }
                else
                {
                    polygon = _geometryFactory.CreatePolygon(outerRing);
                }

                if (!polygon.IsValid)
                {
                    _logger.LogWarning($"Polygon for element {element.Id} is invalid");
                }

                return polygon;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create polygon for element {element.Id}");
                return null;
            }
        }

        private static List<List<(long id, List<Coordinate> coords)>> GroupConnectedWays(List<(long id, List<Coordinate> coords)> ways)
        {
            var groups = new List<List<(long id, List<Coordinate> coords)>>();
            var remaining = new List<(long id, List<Coordinate> coords)>(ways);

            while (remaining.Count != 0)
            {
                var group = new List<(long id, List<Coordinate> coords)>();
                var current = remaining[0];
                group.Add(current);
                remaining.RemoveAt(0);

                bool foundConnection;
                do
                {
                    foundConnection = false;
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        var way = remaining[i];

                        foreach (var (id, coords) in group)
                        {
                            if (WaysConnect(coords, way.coords))
                            {
                                group.Add(way);
                                remaining.RemoveAt(i);
                                foundConnection = true;
                                break;
                            }
                        }

                        if (foundConnection) break;
                    }
                } while (foundConnection && remaining.Count != 0);

                groups.Add(group);
            }

            return groups;
        }

        private static bool WaysConnect(List<Coordinate> way1, List<Coordinate> way2)
        {
            if (way1.Count == 0 || way2.Count == 0) return false;

            return AreCoordinatesEqual(way1.First(), way2.First()) ||
                   AreCoordinatesEqual(way1.First(), way2.Last()) ||
                   AreCoordinatesEqual(way1.Last(), way2.First()) ||
                   AreCoordinatesEqual(way1.Last(), way2.Last());
        }

        private static List<Coordinate> RemoveConsecutiveDuplicates(List<Coordinate> coords)
        {
            if (coords.Count <= 1) return coords;

            var result = new List<Coordinate> { coords[0] };

            for (int i = 1; i < coords.Count; i++)
            {
                if (!AreCoordinatesEqual(coords[i], result.Last()))
                {
                    result.Add(coords[i]);
                }
            }

            return result;
        }

        private static bool AreCoordinatesEqual(Coordinate c1, Coordinate c2, double tolerance = 0.000001)
        {
            return Math.Abs(c1.X - c2.X) < tolerance && Math.Abs(c1.Y - c2.Y) < tolerance;
        }

        private LinearRing? BuildRingFromWays(List<(long id, List<Coordinate> coords)> ways, long elementId)
        {
            if (ways.Count == 0) return null;

            var orderedCoords = new List<Coordinate>();
            var remainingWays = new List<(long id, List<Coordinate> coords)>(ways);

            var (id, coords) = remainingWays[0];
            orderedCoords.AddRange(coords);
            remainingWays.RemoveAt(0);

            int maxIterations = ways.Count * 2;
            int iterations = 0;

            while (remainingWays.Count != 0 && iterations < maxIterations)
            {
                iterations++;
                bool foundConnection = false;

                var lastCoord = orderedCoords.Last();
                var firstCoord = orderedCoords.First();

                for (int i = 0; i < remainingWays.Count; i++)
                {
                    var way = remainingWays[i];
                    var wayFirst = way.coords.First();
                    var wayLast = way.coords.Last();

                    if (AreCoordinatesEqual(lastCoord, wayFirst))
                    {
                        orderedCoords.AddRange(way.coords.Skip(1));
                        remainingWays.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    else if (AreCoordinatesEqual(lastCoord, wayLast))
                    {
                        var reversed = way.coords.ToList();
                        reversed.Reverse();
                        orderedCoords.AddRange(reversed.Skip(1));
                        remainingWays.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    else if (AreCoordinatesEqual(firstCoord, wayLast))
                    {
                        orderedCoords.InsertRange(0, way.coords.Take(way.coords.Count - 1));
                        remainingWays.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    else if (AreCoordinatesEqual(firstCoord, wayFirst))
                    {
                        var reversed = way.coords.ToList();
                        reversed.Reverse();
                        orderedCoords.InsertRange(0, reversed.Take(reversed.Count - 1));
                        remainingWays.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                }

                if (!foundConnection)
                {
                    _logger.LogWarning($"Could not find connection for remaining {remainingWays.Count} ways in element {elementId}");
                    break;
                }
            }

            if (orderedCoords.Count >= 3)
            {
                if (!AreCoordinatesEqual(orderedCoords.First(), orderedCoords.Last()))
                {
                    orderedCoords.Add(orderedCoords.First());
                }

                if (orderedCoords.Count >= 4)
                {
                    try
                    {
                        var cleanedCoords = RemoveConsecutiveDuplicates(orderedCoords);

                        if (cleanedCoords.Count >= 4)
                        {
                            return _geometryFactory.CreateLinearRing([.. cleanedCoords]);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to create linear ring for element {elementId}");
                    }
                }
            }

            return null;
        }

        private async Task SaveRegionsToDatabase(List<Region> regions)
        {
            var newRegions = regions.Where(r => r.Id == 0).ToList();

            if (newRegions.Count != 0)
            {
                await _context.Regions.AddRangeAsync(newRegions);
                _logger.LogInformation($"Adding {newRegions.Count} new regions to database");
            }

            var existingRegions = regions.Where(r => r.Id != 0).ToList();
            if (existingRegions.Count != 0)
            {
                _logger.LogInformation($"Updating {existingRegions.Count} existing regions");
            }

            await _context.SaveChangesAsync();
        }

        private async Task<OverpassResponse?> ExecuteOverpassQueryAsync(string query)
        {
            int maxRetries = 3;
            int currentRetry = 0;
            TimeSpan retryDelay = TimeSpan.FromSeconds(5);

            while (currentRetry <= maxRetries)
            {
                try
                {
                    _logger.LogInformation($"Executing Overpass query (attempt {currentRetry + 1}/{maxRetries + 1})");

                    var content = new StringContent(
                        $"data={Uri.EscapeDataString(query)}",
                        System.Text.Encoding.UTF8,
                        "application/x-www-form-urlencoded"
                    );

                    var response = await _httpClient.PostAsync(OVERPASS_API_URL, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        return JsonSerializer.Deserialize<OverpassResponse>(jsonResponse, JsonOptions);
                    }

                    if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    {
                        currentRetry++;
                        if (currentRetry <= maxRetries)
                        {
                            _logger.LogWarning($"Overpass API returned {response.StatusCode}, retrying in {retryDelay.TotalSeconds} seconds...");
                            await Task.Delay(retryDelay);
                            retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
                            continue;
                        }
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Overpass API request failed with status {response.StatusCode}. Response: {errorContent}");
                    return null;
                }
                catch (TaskCanceledException)
                {
                    _logger.LogError("Overpass API request timed out");
                    currentRetry++;
                    if (currentRetry <= maxRetries)
                    {
                        await Task.Delay(retryDelay);
                        continue;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling Overpass API");
                    return null;
                }
            }

            return null;
        }
    }
}