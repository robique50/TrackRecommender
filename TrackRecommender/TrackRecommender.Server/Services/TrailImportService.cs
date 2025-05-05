using System.Text;
using System.Text.Json;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Properties.Repositories.Interfaces;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService
    {
        private readonly ITrailRepository _trailRepository;
        private readonly HttpClient _httpClient;

        public TrailImportService(ITrailRepository trailRepository, HttpClient httpClient)
        {
            _trailRepository = trailRepository;
            _httpClient = httpClient;
        }

        public async Task ImportTrailsFromOverpassAsync(string regionBoundingBox)
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

            var content = new StringContent(overpassQuery, Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                await ProcessTrailsDataAsync(jsonResponse);
            }
        }
        private string DetermineRegionFromCoordinates(List<long> nodeIds, Dictionary<long, NodeCoordinates> nodesDict)
        {
            if (!nodeIds.Any() || !nodeIds.All(nodeId => nodesDict.ContainsKey(nodeId)))
                return "Other";

            double avgLat = 0;
            double avgLon = 0;

            foreach (var nodeId in nodeIds)
            {
                avgLat += nodesDict[nodeId].Lat;
                avgLon += nodesDict[nodeId].Lon;
            }

            avgLat /= nodeIds.Count;
            avgLon /= nodeIds.Count;

            var regions = new Dictionary<string, (double minLat, double maxLat, double minLon, double maxLon)>
    {
        { "Bucegi", (45.33, 45.53, 25.38, 25.60) },
        { "Fagaras", (45.50, 45.70, 24.50, 25.20) },
        { "Retezat", (45.30, 45.42, 22.70, 23.00) },
        { "Piatra Craiului", (45.47, 45.58, 25.17, 25.35) },
        { "Ceahlau", (46.92, 47.05, 25.85, 26.05) },
        { "Apuseni", (46.30, 46.70, 22.50, 23.20) },
        { "Rodnei", (47.45, 47.65, 24.65, 25.00) },
        { "Ciucas", (45.48, 45.55, 25.90, 26.05) },
        { "Postavaru", (45.55, 45.62, 25.52, 25.60) },
        { "Calimani", (47.05, 47.20, 25.10, 25.35) },
        { "Parang", (45.30, 45.40, 23.45, 23.70) },
        { "Cozia", (45.28, 45.35, 24.28, 24.40) },
        { "Iezer-Papusa", (45.45, 45.55, 24.90, 25.10) },
        { "Harghita", (46.35, 46.50, 25.50, 25.75) },
        { "Bihor", (46.50, 46.60, 22.60, 22.80) },
        { "Cindrel", (45.50, 45.70, 23.70, 24.00) },
        { "Baiului", (45.40, 45.50, 25.60, 25.75) },
        { "Lotru", (45.35, 45.45, 23.95, 24.30) },
        { "Macin", (45.15, 45.30, 28.15, 28.35) },
        { "Gutai", (47.70, 47.80, 23.70, 23.90) },
        { "Semenic", (45.15, 45.25, 22.00, 22.15) },
        { "Poiana Rusca", (45.60, 45.75, 22.30, 22.55) },
        { "Zarand", (46.10, 46.20, 22.20, 22.40) },
        { "Vladeasa", (46.75, 46.90, 22.75, 22.90) },
        { "Hasmas", (46.65, 46.75, 25.75, 25.90) },
        { "Rarau-Giumalau", (47.45, 47.55, 25.50, 25.70) }
    };

            foreach (var region in regions)
            {
                var (minLat, maxLat, minLon, maxLon) = region.Value;
                if (avgLat >= minLat && avgLat <= maxLat && avgLon >= minLon && avgLon <= maxLon)
                    return region.Key;
            }

            return "Unknown";
        }

        private async Task ProcessTrailsDataAsync(string jsonData)
        {
            var osmData = JsonSerializer.Deserialize<OverpassResponse>(jsonData);

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

                string region = DetermineRegionFromCoordinates(element.Nodes, nodesDict);

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
                    region: region
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