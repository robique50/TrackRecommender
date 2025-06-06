namespace TrackRecommender.Server.Models.DTOs
{
    public class PreferenceOptionsDto
    {
        public List<string> TrailTypes { get; set; } = [];
        public List<string> Difficulties { get; set; } = [];
        public List<string> Categories { get; set; } = [];
        public List<string> AvailableTags { get; set; } = [];
        public List<RegionOptionDto> Regions { get; set; } = [];
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }
        public double MinDuration { get; set; }
        public double MaxDuration { get; set; }
    }
}
