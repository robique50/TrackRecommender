namespace TrackRecommender.Server.Models
{
    public class UserPreferences
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public List<string> PreferredTrailTypes { get; set; }
        public string PreferredDifficulty { get; set; }
        public List<string> PreferredTags { get; set; }
        public double MaxDistance { get; set; }

        public UserPreferences(int id, int userId, List<string> preferredTrailTypes, string preferredDifficulty,
            List<string> preferredTags, double maxDistance)
        {
            Id = id;
            UserId = userId;
            PreferredTrailTypes = preferredTrailTypes;
            PreferredDifficulty = preferredDifficulty;
            PreferredTags = preferredTags;
            MaxDistance = maxDistance;
        }
    }
}