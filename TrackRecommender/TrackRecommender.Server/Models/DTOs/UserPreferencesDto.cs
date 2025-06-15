using System.Text.Json.Serialization;

namespace TrackRecommender.Server.Models.DTOs
{
    public class UserPreferencesDto
    {
        public List<string>? PreferredTrailTypes { get; set; }
        public string? PreferredDifficulty { get; set; }
        public List<string>? PreferredTags { get; set; }
        public double? MaxDistance { get; set; }
        public double? MaxDuration { get; set; }
        public List<string>? PreferredCategories { get; set; }
        public int? MinimumRating { get; set; } 
        public List<string>? PreferredRegionNames { get; set; }
        [JsonPropertyName("preferredMarkings")]
        public List<TrailMarkingDto>? PreferredMarkings { get; set; }
    }
}
