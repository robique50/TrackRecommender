namespace TrackRecommender.Server.Models
{
    public class TrailRegion
    {
        public int TrailId { get; set; }
        public required Trail Trail { get; set; }

        public int RegionId { get; set; }
        public required Region Region { get; set; }
    }
}
