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
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Role { get; set; } = "User";
        public UserPreferences? Preferences { get; set; }
        public ICollection<UserTrailRating> TrailRatings { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public User()
        {
            Username = string.Empty;
            Email = string.Empty;
            PasswordHash = string.Empty;
            CreatedAt = DateTime.UtcNow;
            TrailRatings = new List<UserTrailRating>();
        }

        public User(string username, string email)
        {
            Username = username;
            Email = email;
            PasswordHash = string.Empty;
            CreatedAt = DateTime.UtcNow;
            Role = "User";
            TrailRatings = new List<UserTrailRating>();
        }

        public void AddRefreshToken(string token, string ipAddress, DateTime expiryDate)
        {
            RefreshTokens.Add(new RefreshToken
            {
                Token = token,
                ExpiryDate = expiryDate,
                CreatedByIp = ipAddress
            });
        }
        public void RevokeRefreshToken(string token, string ipAddress, string replacementToken)
        {
            var refreshToken = RefreshTokens.SingleOrDefault(r => r.Token == token && r.IsActive);
            if (refreshToken == null) return;

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = replacementToken;
        }
    }
}