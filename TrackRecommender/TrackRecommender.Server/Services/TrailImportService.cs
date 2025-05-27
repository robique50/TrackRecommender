using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using System.Text.Json;
using TrackRecommender.Server.Models;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TrailImportService> _logger;
        private readonly GeometryFactory _geometryFactory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private int _newTrailCounter = 0;
        private int _updatedTrailCounter = 0;
        private Dictionary<int, Region> _regionsCache = [];

        private const int OVERPASS_TIMEOUT = 300;
        private const int BATCH_SIZE = 50;
        private const double MIN_DISTANCE_KM = 3.0;
        private const double MAX_DISTANCE_KM = 2000;
        private const double SIMPLIFY_TOLERANCE = 0.00005;
        private const double CONNECTION_TOLERANCE = 0.001;

        public TrailImportService(
            AppDbContext context,
            HttpClient httpClient,
            ILogger<TrailImportService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _logger = logger;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _httpClient.Timeout = TimeSpan.FromSeconds(OVERPASS_TIMEOUT + 60);
        }

        public async Task ImportAllTrailsAsync()
        {
            _newTrailCounter = 0;
            _updatedTrailCounter = 0;

            try
            {
                await LoadRegionsCache();

                if (_regionsCache.Count == 0)
                {
                    _logger.LogError("No regions found in database. Please import regions first.");
                    return;
                }

                await ImportTrailsByNetwork("iwn|nwn", "International/National");
                await ImportTrailsByNetwork("rwn", "Regional");
                await ImportTrailsByNetwork("lwn", "Local");
                await ImportTrailsByNetwork(null, "All other marked trails");

                _logger.LogInformation($"Import completed. New: {_newTrailCounter}, Updated: {_updatedTrailCounter}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during trail import");
                throw;
            }
        }

        private async Task LoadRegionsCache()
        {
            var regions = await _context.Regions
                .Where(r => r.Boundary != null)
                .AsNoTracking()
                .ToListAsync();

            _regionsCache = regions.ToDictionary(r => r.Id, r => r);
            _logger.LogInformation($"Loaded {_regionsCache.Count} regions into cache");
        }

        private async Task ImportTrailsByNetwork(string? networkFilter, string description)
        {
            _logger.LogInformation($"Importing {description}...");

            var query = BuildQuery(networkFilter);
            var response = await ExecuteOverpassQuery(query);

            if (response?.Elements == null || response.Elements.Count == 0)
            {
                _logger.LogInformation($"No trails found for {description}");
                return;
            }

            await ProcessTrails(response);
        }

        private static string BuildQuery(string? networkFilter)
        {
            var networkClause = string.IsNullOrEmpty(networkFilter)
                ? ""
                : $"[\"network\"~\"^({networkFilter})$\"]";

            return $@"
                [out:json][timeout:{OVERPASS_TIMEOUT}];
                area[""name""=""România""][""admin_level""=""2""]->.searchArea;
                (
                    relation(area.searchArea)[""type""=""route""][""route""~""^(hiking|foot|bicycle|mtb)$""][""name""]{networkClause};
                );
                (._;>>;);
                out geom;";
        }

        private async Task ProcessTrails(OverpassResponse response)
        {
            var relations = response.Elements
                .Where(e => e.Type == "relation" && e.Tags?.ContainsKey("name") == true)
                .ToList();

            var waysDict = response.Elements
                .Where(e => e.Type == "way")
                .ToDictionary(w => w.Id, w => w);

            _logger.LogInformation($"Processing {relations.Count} trails...");

            var batch = new List<Trail>();

            foreach (var relation in relations)
            {
                try
                {
                    var trail = CreateTrailFromRelation(relation, waysDict);
                    if (trail == null) continue;

                    var existing = await _context.Trails
                        .Include(t => t.TrailRegions)
                        .FirstOrDefaultAsync(t => t.OsmId == relation.Id);

                    if (existing != null)
                    {
                        UpdateTrail(existing, trail);
                        _updatedTrailCounter++;
                    }
                    else
                    {
                        batch.Add(trail);
                        _newTrailCounter++;
                    }

                    if (batch.Count >= BATCH_SIZE)
                    {
                        await SaveBatch(batch);
                        batch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing trail {relation.Tags?.GetValueOrDefault("name")}");
                }
            }

            if (batch.Count != 0)
            {
                await SaveBatch(batch);
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }
        }

        private Trail? CreateTrailFromRelation(OverpassElement relation, Dictionary<long, OverpassElement> waysDict)
        {
            var tags = relation.Tags ?? [];
            var name = tags.GetValueOrDefault("name");

            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (IsNonRomanianTrail(name, tags))
                return null;

            var lineString = BuildLineString(relation, waysDict);
            if (lineString == null || lineString.Coordinates.Length < 2)
                return null;
 
            var distance = CalculateDistance(lineString);
            if (distance < MIN_DISTANCE_KM || distance > MAX_DISTANCE_KM)
                return null;

            if (lineString.Coordinates.Length > 500)
            {
                var tolerance = distance > 50 ? SIMPLIFY_TOLERANCE * 2 : SIMPLIFY_TOLERANCE;
                lineString = SimplifyLineString(lineString, tolerance);
            }

            var intersectedRegions = GetIntersectedRegions(lineString);
            if (intersectedRegions.Count == 0)
                return null;

            var trail = new Trail
            {
                OsmId = relation.Id,
                Name = name,
                Description = tags.GetValueOrDefault("description", ""),
                Coordinates = lineString,
                Distance = distance,
                Difficulty = DetermineDifficulty(tags, distance),
                TrailType = DetermineTrailType(tags),
                Duration = EstimateDuration(distance, tags),
                StartLocation = FormatCoordinate(lineString.StartPoint),
                EndLocation = FormatCoordinate(lineString.EndPoint),
                Tags = ExtractTags(tags),
                Category = DetermineCategory(tags.GetValueOrDefault("network")),
                Network = tags.GetValueOrDefault("network", ""),
                LastUpdated = DateTime.UtcNow
            };

            trail.TrailRegions = [.. intersectedRegions.Select(r => new TrailRegion { RegionId = r.Id, TrailId = trail.Id })];

            return trail;
        }

        private LineString? BuildLineString(OverpassElement relation, Dictionary<long, OverpassElement> waysDict)
        {
            if (relation.Members == null || relation.Members.Count == 0)
                return null;

            var waySegments = new List<(long id, List<Coordinate> coords)>();

            var orderedMembers = relation.Members
                .Where(m => m.Type == "way")
                .OrderBy(m => m.Role == "main" ? 0 : 1)
                .ToList();

            foreach (var member in orderedMembers)
            {
                if (!waysDict.TryGetValue(member.Ref, out var way) || way.Geometry == null)
                    continue;

                var coords = way.Geometry
                    .Select(g => new Coordinate(g.Lon, g.Lat))
                    .ToList();

                if (coords.Count >= 2)
                {
                    var cleanCoords = new List<Coordinate> { coords[0] };
                    for (int i = 1; i < coords.Count; i++)
                    {
                        if (!IsClose(coords[i], coords[i - 1], 0.00001))
                        {
                            cleanCoords.Add(coords[i]);
                        }
                    }

                    if (cleanCoords.Count >= 2)
                        waySegments.Add((member.Ref, cleanCoords));
                }
            }

            if (waySegments.Count == 0)
                return null;

            var connected = ConnectSegments(waySegments);
            if (connected == null || connected.Count < 2)
                return null;

            try
            {
                var lineString = _geometryFactory.CreateLineString([.. connected]);

                if (!lineString.IsValid)
                {
                    _logger.LogWarning($"Invalid LineString created for relation {relation.Id}");
                    return null;
                }

                return lineString;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to create LineString for relation {relation.Id}");
                return null;
            }
        }

        private List<Coordinate>? ConnectSegments(List<(long id, List<Coordinate> coords)> segments)
{
    if (segments.Count == 0)
        return null;

    var result = new List<Coordinate>(segments[0].coords);
    var used = new HashSet<long> { segments[0].id };

    while (used.Count < segments.Count)
    {
        var connected = false;
        var lastPoint = result.Last();
        var firstPoint = result.First();

        foreach (var (id, coords) in segments.Where(s => !used.Contains(s.id)))
        {
            if (IsClose(lastPoint, coords.First(), CONNECTION_TOLERANCE))
            {
                result.AddRange(coords.Skip(1));
                used.Add(id);
                connected = true;
                break;
            }
            else if (IsClose(lastPoint, coords.Last(), CONNECTION_TOLERANCE))
            {
                var reversed = coords.ToList();
                reversed.Reverse();
                result.AddRange(reversed.Skip(1));
                used.Add(id);
                connected = true;
                break;
            }
        }

        if (!connected)
        {
            foreach (var (id, coords) in segments.Where(s => !used.Contains(s.id)))
            {
                if (IsClose(firstPoint, coords.Last(), CONNECTION_TOLERANCE))
                {
                    result.InsertRange(0, coords.Take(coords.Count - 1));
                    used.Add(id);
                    connected = true;
                    break;
                }
                else if (IsClose(firstPoint, coords.First(), CONNECTION_TOLERANCE))
                {
                    var reversed = coords.ToList();
                    reversed.Reverse();
                    result.InsertRange(0, reversed.Take(reversed.Count - 1));
                    used.Add(id);
                    connected = true;
                    break;
                }
            }
        }

        if (!connected)
        {
            _logger.LogWarning($"Could not connect all segments. Using longest segment with {result.Count} points out of {segments.Count} total segments.");
            break;
        }
    }

    return result.Count >= 2 ? result : null;
}

        private static bool IsClose(Coordinate c1, Coordinate c2, double tolerance = 0.001)
        {
            return Math.Abs(c1.X - c2.X) < tolerance && Math.Abs(c1.Y - c2.Y) < tolerance;
        }

        private static double CalculateDistance(LineString lineString)
        {
            var totalDistance = 0.0;
            var coords = lineString.Coordinates;

            for (int i = 0; i < coords.Length - 1; i++)
            {
                var distance = HaversineDistance(
                    coords[i].Y, coords[i].X,
                    coords[i + 1].Y, coords[i + 1].X
                );

                if (double.IsFinite(distance))
                {
                    totalDistance += distance;
                }
            }

            return Math.Round(totalDistance, 2);
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        private LineString SimplifyLineString(LineString original, double? customTolerance = null)
        {
            try
            {
                var tolerance = customTolerance ?? SIMPLIFY_TOLERANCE;
                var simplifier = new DouglasPeuckerSimplifier(original)
                {
                    DistanceTolerance = tolerance
                };

                if (simplifier.GetResultGeometry() is LineString simplified && simplified.Coordinates.Length >= 2)
                {
                    _logger.LogDebug($"Simplified LineString from {original.Coordinates.Length} to {simplified.Coordinates.Length} points");
                    return simplified;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to simplify LineString");
            }

            return original;
        }

        private List<Region> GetIntersectedRegions(LineString lineString)
        {
            var intersected = new List<Region>();

            foreach (var region in _regionsCache.Values)
            {
                if (region.Boundary == null || !region.Boundary.IsValid)
                    continue;

                try
                {
                    if (lineString.Intersects(region.Boundary))
                        intersected.Add(region);
                }
                catch { }
            }

            return intersected;
        }

        private static bool IsNonRomanianTrail(string name, Dictionary<string, string> tags)
        {
            var keywords = new[] { "ukraine", "hungary", "serbia", "bulgaria", "moldova" };
            var nameLower = name.ToLowerInvariant();

            return keywords.Any(k => nameLower.Contains(k)) ||
                   tags.Any(t => t.Key.StartsWith("name:") &&
                            keywords.Any(k => t.Value.Contains(k, StringComparison.OrdinalIgnoreCase)));

        }

        private static string FormatCoordinate(Point point)
        {
            return $"{point.Y:F6},{point.X:F6}";
        }

        private static string DetermineDifficulty(Dictionary<string, string> tags, double distance)
        {
            if (tags.TryGetValue("sac_scale", out var sac))
            {
                return sac switch
                {
                    "hiking" => "Easy",
                    "mountain_hiking" => "Moderate",
                    "demanding_mountain_hiking" => "Difficult",
                    "alpine_hiking" => "Very Difficult",
                    "demanding_alpine_hiking" => "Extreme",
                    "difficult_alpine_hiking" => "Extreme",
                    _ => "Moderate"
                };
            }

            if (distance < 5) return "Easy";
            if (distance < 10) return "Moderate";
            if (distance < 20) return "Difficult";
            if (distance < 50) return "Very Difficult";
            return "Extreme";
        }

        private static string DetermineTrailType(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("route", out var route))
            {
                return route switch
                {
                    "hiking" => "Hiking",
                    "foot" => "Walking",
                    "bicycle" => "Cycling",
                    "mtb" => "Mountain Biking",
                    _ => "Hiking"
                };
            }
            return "Hiking";
        }

        private static string DetermineCategory(string? network)
        {
            if (string.IsNullOrEmpty(network))
                return "Local";

            var n = network.ToLowerInvariant();
            if (n.Contains("iwn") || n.Contains("icn")) return "International";
            if (n.Contains("nwn") || n.Contains("ncn")) return "National";
            if (n.Contains("rwn") || n.Contains("rcn")) return "Regional";
            return "Local";
        }

        private static double EstimateDuration(double distance, Dictionary<string, string> tags)
        {
            if (!double.IsFinite(distance) || distance <= 0)
                return 1.0;
        
            var hours = distance / 3.5;
            var result = Math.Round(hours * 1.2, 1);
    
            return double.IsFinite(result) ? result : 1.0;
        }

        private static List<string> ExtractTags(Dictionary<string, string> tags)
        {
            var relevant = new[] { "name", "ref", "network", "route", "operator", "sac_scale" };

            return [.. tags
                .Where(t => relevant.Contains(t.Key) && !string.IsNullOrWhiteSpace(t.Value))
                .Select(t => $"{t.Key}={t.Value}")];
        }

        private static void UpdateTrail(Trail existing, Trail updated)
        {
            existing.Name = updated.Name;
            existing.Description = updated.Description;
            existing.Coordinates = updated.Coordinates;
            existing.Distance = updated.Distance;
            existing.Difficulty = updated.Difficulty;
            existing.TrailType = updated.TrailType;
            existing.Duration = updated.Duration;
            existing.StartLocation = updated.StartLocation;
            existing.EndLocation = updated.EndLocation;
            existing.Tags = updated.Tags;
            existing.Category = updated.Category;
            existing.Network = updated.Network;
            existing.LastUpdated = DateTime.UtcNow;

            existing.TrailRegions.Clear();
            foreach (var tr in updated.TrailRegions)
            {
                existing.TrailRegions.Add(new TrailRegion { TrailId = existing.Id, RegionId = tr.RegionId });
            }
        }

        private async Task SaveBatch(List<Trail> trails)
        {
            if (trails.Count == 0)
                return;

            await _context.Trails.AddRangeAsync(trails);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Saved batch of {trails.Count} trails");
        }

        private async Task<OverpassResponse?> ExecuteOverpassQuery(string query)
        {
            try
            {
                var content = new StringContent(
                    $"data={Uri.EscapeDataString(query)}",
                    System.Text.Encoding.UTF8,
                    "application/x-www-form-urlencoded"
                );

                var response = await _httpClient.PostAsync(
                    "https://overpass-api.de/api/interpreter",
                    content
                );

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OverpassResponse>(jsonResponse, JsonOptions);
                }

                _logger.LogError($"Overpass API returned {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Overpass API");
                return null;
            }
        }
    }
}