using System.Text.Json.Serialization;

namespace TrackRecommender.Server.External.OpenStreetMap
{
    public class OverpassModels
    {
        public class OverpassResponse
        {
            [JsonPropertyName("elements")]
            public List<OverpassElement> Elements { get; set; }
        }

        public class OverpassElement
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("lat")]
            public double? Lat { get; set; }

            [JsonPropertyName("lon")]
            public double? Lon { get; set; }

            [JsonPropertyName("nodes")]
            public List<long> Nodes { get; set; }

            [JsonPropertyName("tags")]
            public Dictionary<string, string> Tags { get; set; }
        }

        public class NodeCoordinates
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }
    }
}
