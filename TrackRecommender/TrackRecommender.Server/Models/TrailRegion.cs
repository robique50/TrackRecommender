namespace TrackRecommender.Server.Models
{
    public class TrailRegion
    {
        public int TrailId { get; set; }
        public Trail? Trail { get; set; }

        public int RegionId { get; set; }
        public Region? Region { get; set; }
    }
}
