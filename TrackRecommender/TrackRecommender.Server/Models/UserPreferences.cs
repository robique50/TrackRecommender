using System.ComponentModel.DataAnnotations.Schema;

namespace TrackRecommender.Server.Models
{
    public class UserPreferences
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public List<string> PreferredTrailTypes { get; set; }
        public string PreferredDifficulty { get; set; }
        public List<string> PreferredTags { get; set; }
        public double MaxDistance { get; set; }
        public double MaxDuration { get; set; }
        public List<string> PreferredCategories { get; set; }
        public List<int> PreferredRegionIds { get; set; }
        public string? MarkingPreferences { get; set; }
        public int MinimumRating { get; set; }

        public UserPreferences()
        {
            PreferredTrailTypes = [];
            PreferredDifficulty = "Easy";
            PreferredTags = [];
            PreferredCategories = [];
            PreferredRegionIds = [];
            MaxDistance = 20;
            MaxDuration = 8;
            MinimumRating = 0;
        }

        public UserPreferences(int id, int userId, List<string> preferredTrailTypes, string preferredDifficulty,
            List<string> preferredTags, double maxDistance)
        {
            Id = id;
            UserId = userId;
            PreferredTrailTypes = preferredTrailTypes;
            PreferredDifficulty = preferredDifficulty;
            PreferredTags = preferredTags;
            MaxDistance = maxDistance;
            PreferredCategories = [];
            PreferredRegionIds = [];
            MaxDuration = 8;
            MinimumRating = 0;
        }
    }
}