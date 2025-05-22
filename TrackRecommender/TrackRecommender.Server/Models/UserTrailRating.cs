using System.ComponentModel.DataAnnotations;

namespace TrackRecommender.Server.Models
{
    public class UserTrailRating
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int TrailId { get; set; }
        public Trail Trail { get; set; } = null!;

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public bool HasCompleted { get; set; }

        public DateTime RatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public double? ActualDuration { get; set; }

        public string? PerceivedDifficulty { get; set; }

        public UserTrailRating()
        {
            RatedAt = DateTime.UtcNow;
            HasCompleted = false;
        }

        public UserTrailRating(int userId, int trailId, int rating)
        {
            UserId = userId;
            TrailId = trailId;
            Rating = rating;
            RatedAt = DateTime.UtcNow;
            HasCompleted = false;
        }
    }
}