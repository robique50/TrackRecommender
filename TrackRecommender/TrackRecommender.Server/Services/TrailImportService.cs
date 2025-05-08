using System.Text;
using System.Text.Json;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService
    {
        private readonly ITrailRepository _trailRepository;
        private readonly IRegionRepository _regionRepository;
        private readonly HttpClient _httpClient;

        public TrailImportService(ITrailRepository trailRepository, IRegionRepository regionRepository, HttpClient httpClient)
        {
            _trailRepository = trailRepository;
            _regionRepository = regionRepository;
            _httpClient = httpClient;
        }

        public async Task ImportTrailsFromOverpassAsync(string regionBoundingBox)
        {
            int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    var overpassQuery = $@"
            [out:json];
            (
              way[highway=path][""sac_scale""][name]({regionBoundingBox});
              way[route=hiking][name]({regionBoundingBox});
            );
            out body;
            >;
            out skel qt;";

                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(2);

                    var content = new StringContent(overpassQuery, Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = await httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        await ProcessTrailsDataAsync(jsonResponse);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw new InvalidOperationException($"Import failed after {maxRetries} attempts: {ex.Message}", ex);

                    await Task.Delay(5000 * retryCount);
                }
            }
        }

        public async Task ImportTrailsFromAllRegionsAsync()
        {
            var regions = await _regionRepository.GetAllRegionsAsync();

            foreach (var region in regions)
            {
                string boundingBox = region.GetBoundingBoxString();
                await ImportTrailsFromOverpassAsync(boundingBox);

                await Task.Delay(5000); 
            }
        }

        private async Task<int> GetRegionIdFromCoordinates(List<long> nodeIds, Dictionary<long, NodeCoordinates> nodesDict)
        {
            if (!nodeIds.Any() || !nodeIds.All(nodeId => nodesDict.ContainsKey(nodeId)))
                return await GetDefaultRegionIdAsync();

            double avgLat = 0;
            double avgLon = 0;

            foreach (var nodeId in nodeIds)
            {
                avgLat += nodesDict[nodeId].Lat;
                avgLon += nodesDict[nodeId].Lon;
            }

            avgLat /= nodeIds.Count;
            avgLon /= nodeIds.Count;

            var regions = await _regionRepository.GetAllRegionsAsync();

            foreach (var region in regions)
            {
                if (avgLat >= region.MinLat && avgLat <= region.MaxLat &&
                    avgLon >= region.MinLon && avgLon <= region.MaxLon)
                    return region.Id;
            }

            return await GetUnknownRegionIdAsync();
        }

        private async Task<int> GetDefaultRegionIdAsync()
        {
            var defaultRegion = await _regionRepository.GetRegionByNameAsync("Other");
            if (defaultRegion == null)
            {
                defaultRegion = new Region
                (
                    id : 0,
                    name : "Other",
                    description : "Default region for trails without a specific region",
                    minLat : 43.5,
                    maxLat : 48.2,
                    minLon : 20.2,
                    maxLon : 30.0
                );
                await _regionRepository.AddRegionAsync(defaultRegion);
                await _regionRepository.SaveChangesAsync();
            }
            return defaultRegion.Id;
        }

        private async Task<int> GetUnknownRegionIdAsync()
        {
            var unknownRegion = await _regionRepository.GetRegionByNameAsync("Unknown");
            if (unknownRegion == null)
            {
                unknownRegion = new Region
                (
                    id : 0,
                    name : "Unknown",
                    description : "Unknown region",
                    minLat : 43.5,
                    maxLat : 48.2,
                    minLon : 20.2,
                    maxLon : 30.0
                );
                await _regionRepository.AddRegionAsync(unknownRegion);
                await _regionRepository.SaveChangesAsync();
            }
            return unknownRegion.Id;
        }

        private async Task ProcessTrailsDataAsync(string jsonData)
        {
            var osmData = JsonSerializer.Deserialize<OverpassResponse>(jsonData);

            if (osmData == null || osmData.Elements == null)
            {
                throw new InvalidOperationException("Failed to deserialize OSM data or Elements collection is null");
            }

            var nodesDict = osmData.Elements
                .Where(e => e.Type == "node")
                .ToDictionary(
                    e => e.Id,
                    e => new NodeCoordinates
                    {
                        Lat = e.Lat ?? 0,
                        Lon = e.Lon ?? 0
                    }
                );

            var processedTrailNames = new HashSet<string>();

            foreach (var element in osmData.Elements.Where(e => e.Type == "way"))
            {
                if (!element.Tags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (processedTrailNames.Contains(name))
                {
                    continue;
                }
                processedTrailNames.Add(name);

                string difficulty = "Moderate";
                if (element.Tags.TryGetValue("sac_scale", out var sac))
                {
                    difficulty = sac switch
                    {
                        "hiking" => "Easy",
                        "mountain_hiking" => "Moderate",
                        "demanding_mountain_hiking" => "Difficult",
                        "alpine_hiking" => "Advanced",
                        "demanding_alpine_hiking" => "Very Difficult",
                        "difficult_alpine_hiking" => "Extreme",
                        _ => "Moderate"
                    };
                }

                string trailType = "Hiking";
                if (element.Tags.TryGetValue("trail_visibility", out var visibility))
                {
                    if (visibility == "excellent" || visibility == "good")
                        trailType = "Well-marked Trail";
                    else if (visibility == "bad" || visibility == "horrible")
                        trailType = "Poorly Marked Trail";
                }
                if (element.Tags.TryGetValue("surface", out var surface))
                {
                    if (surface == "dirt" || surface == "ground")
                        trailType = "Forest Trail";
                    else if (surface == "rock" || surface == "stone")
                        trailType = "Mountain Trail";
                }

                // Obține ID-ul regiunii pe baza coordonatelor
                int regionId = await GetRegionIdFromCoordinates(element.Nodes, nodesDict);

                var relevantTags = new List<string>();
                string[] usefulTagKeys = { "bicycle", "foot", "surface", "trail_visibility", "marked_trail" };
                foreach (var tag in element.Tags)
                {
                    if (usefulTagKeys.Contains(tag.Key))
                        relevantTags.Add($"{tag.Key}: {tag.Value}");
                }

                string description = string.Empty;

                if (!element.Nodes.All(nodeId => nodesDict.ContainsKey(nodeId)))
                    continue;

                var coordinates = element.Nodes
                    .Select(nodeId => new[] { nodesDict[nodeId].Lon, nodesDict[nodeId].Lat })
                    .ToList();

                var geoJson = new
                {
                    type = "LineString",
                    coordinates = coordinates
                };

                double distance = CalculateDistance(element.Nodes, nodesDict);

                if (distance < 0.5)
                    continue;

                double duration = EstimateDuration(distance, difficulty);

                var trail = new Trail(
                    id: 0,
                    name: name,
                    description: description,
                    distance: distance,
                    duration: duration,
                    difficulty: difficulty,
                    trailType: trailType,
                    startLocation: $"{nodesDict[element.Nodes.First()].Lat:F6}, {nodesDict[element.Nodes.First()].Lon:F6}",
                    endLocation: $"{nodesDict[element.Nodes.Last()].Lat:F6}, {nodesDict[element.Nodes.Last()].Lon:F6}",
                    geoJsonData: JsonSerializer.Serialize(geoJson),
                    tags: relevantTags,
                    regionId: regionId
                );

                bool trailExists = await _trailRepository.TrailExistsByNameAsync(trail.Name);
                if (!trailExists)
                {
                    await _trailRepository.AddTrailAsync(trail);
                }
            }

            await _trailRepository.SaveChangesAsync();
        }

        private double CalculateDistance(List<long> nodeIds, Dictionary<long, NodeCoordinates> nodesDict)
        {
            double distance = 0;
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                var node1 = nodesDict[nodeIds[i]];
                var node2 = nodesDict[nodeIds[i + 1]];

                distance += CalculateHaversineDistance(
                    node1.Lat, node1.Lon,
                    node2.Lat, node2.Lon
                );
            }

            return distance;
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadius = 6371;

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadius * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private double EstimateDuration(double distance, string difficulty)
        {
            double speed = difficulty switch
            {
                "Easy" => 4.0,
                "Moderate" => 3.0,
                "Difficult" => 2.0,
                "Advanced" => 1.5,
                "Very Difficult" => 1.2,
                "Extreme" => 1.0,
                _ => 3.0
            };

            return distance / speed;
        }
    }
}