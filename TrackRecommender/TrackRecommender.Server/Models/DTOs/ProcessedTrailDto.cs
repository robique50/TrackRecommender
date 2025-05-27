using NetTopologySuite.Geometries;

namespace TrackRecommender.Server.Models.DTOs
{
    public class ProcessedTrailDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public required LineString Coordinates { get; set; }
        public double DistanceKm { get; set; }
        public required string Difficulty { get; set; }
        public required string TrailType { get; set; }
        public double EstimatedDurationHours { get; set; }
        public required string StartLocation { get; set; }
        public required string EndLocation { get; set; }
        public required List<string> Tags { get; set; }
        public required string Category { get; set; }
        public string? Network { get; set; }
        public required List<Region> AssociatedRegions { get; set; }
        public required Dictionary<string, string> OsmTags { get; set; }
    }
}
