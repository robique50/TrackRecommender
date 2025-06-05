using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace TrackRecommender.Server.Models.DTOs
{
    public class TrailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string TrailType { get; set; } = string.Empty;
        public string StartLocation { get; set; } = string.Empty;
        public string EndLocation { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Network { get; set; }
        [JsonIgnore]
        public Geometry? Coordinates { get; set; }
        public string GeoJsonData { get; set; } = string.Empty;
        public List<string> RegionNames { get; set; } = [];
        public List<int> RegionIds { get; set; } = [];
        public List<string> Tags { get; set; } = [];
        public int MatchScore { get; set; } = 0;
        public double AverageRating { get; set; }
        public int ReviewsCount { get; set; }
        public long OsmId { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}