using System.ComponentModel.DataAnnotations;

namespace TrackRecommender.Server.Models.DTOs
{
    public class CreateReviewDto
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string? Comment { get; set; }

        public bool HasCompleted { get; set; } = false;

        public DateTime? CompletedAt { get; set; }

        [Range(0.1, 48.0, ErrorMessage = "Duration must be between 0.1 and 48 hours")]
        public double? ActualDuration { get; set; }

        [StringLength(50)]
        public string? PerceivedDifficulty { get; set; }
    }
}