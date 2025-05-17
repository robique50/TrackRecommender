using System.ComponentModel.DataAnnotations;

namespace TrackRecommender.Server.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Role { get; set; } = "User";
        public UserPreferences? Preferences { get; set; }
        public ICollection<UserTrailRating> TrailRatings { get; set; } = new List<UserTrailRating>();

        public User()
        {
            Username = string.Empty;
            Email = string.Empty;
            PasswordHash = string.Empty;
            PasswordSalt = string.Empty;
            CreatedAt = DateTime.UtcNow;
            TrailRatings = new List<UserTrailRating>();
        }

        public User(string username, string email)
        {
            Username = username;
            Email = email;
            PasswordHash = string.Empty;
            PasswordSalt = string.Empty;
            CreatedAt = DateTime.UtcNow;
            Role = "User";
            TrailRatings = new List<UserTrailRating>();
        }
    }
}