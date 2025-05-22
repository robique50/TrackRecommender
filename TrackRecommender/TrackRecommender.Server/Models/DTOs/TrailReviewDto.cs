namespace TrackRecommender.Server.Models.DTOs
{
    public class TrailReviewDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int TrailId { get; set; }
        public string TrailName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public bool HasCompleted { get; set; }
        public DateTime RatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? ActualDuration { get; set; }
        public string? PerceivedDifficulty { get; set; }
    }
}
