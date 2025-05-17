using System.Text.Json.Serialization;

namespace TrackRecommender.Server.External.OpenStreetMap
{
    public class OverpassModels
    {
        public class OverpassResponse
        {
            [JsonPropertyName("elements")]
            public List<OverpassElement> Elements { get; set; } = new List<OverpassElement>();
        }

        public class OverpassElement
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("lat")]
            public double? Lat { get; set; } 

            [JsonPropertyName("lon")]
            public double? Lon { get; set; } 

            [JsonPropertyName("nodes")]
            public List<long>? Nodes { get; set; } 

            [JsonPropertyName("tags")]
            public Dictionary<string, string>? Tags { get; set; }

            [JsonPropertyName("members")]
            public List<RelationMember>? Members { get; set; } 

            [JsonPropertyName("geometry")]
            public List<GeometryPoint>? Geometry { get; set; }
        }

        public class RelationMember
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("ref")]
            public long Ref { get; set; }

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;
        }

        public class NodeCoordinates 
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
        }

        public class GeometryPoint
        {
            [JsonPropertyName("lat")]
            public double Lat { get; set; }

            [JsonPropertyName("lon")]
            public double Lon { get; set; }
        }
    }
}
