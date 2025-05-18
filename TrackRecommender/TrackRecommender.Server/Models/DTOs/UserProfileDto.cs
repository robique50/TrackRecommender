namespace TrackRecommender.Server.Models.DTOs
{
    public class UserProfileDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool HasPreferences { get; set; }
    }
}