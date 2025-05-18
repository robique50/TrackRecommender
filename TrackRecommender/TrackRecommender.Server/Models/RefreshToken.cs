using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace TrackRecommender.Server.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public string Token { get; set; } = string.Empty;

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime ExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedByIp { get; set; } = string.Empty;

        public DateTime? RevokedAt { get; set; }

        public string? RevokedByIp { get; set; }

        public string? ReplacedByToken { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiryDate;

        public bool IsActive => RevokedAt == null && !IsExpired;
    }
}
