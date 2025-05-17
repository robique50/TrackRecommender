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
        [NotMapped]
        public List<int> PreferredRegionIds { get; set; }
        public int MinimumRating { get; set; }

        public UserPreferences()
        {
            PreferredTrailTypes = new List<string>();
            PreferredDifficulty = "Easy";
            PreferredTags = new List<string>();
            PreferredCategories = new List<string>();
            PreferredRegionIds = new List<int>();
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
            PreferredCategories = new List<string>();
            PreferredRegionIds = new List<int>();
            MaxDuration = 8;
            MinimumRating = 0;
        }
    }
}