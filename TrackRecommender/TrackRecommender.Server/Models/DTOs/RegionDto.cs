namespace TrackRecommender.Server.Models.DTOs
{
    public class RegionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TrailCount { get; set; }
        public string BoundaryGeoJson { get; set; } = string.Empty;
    }
}