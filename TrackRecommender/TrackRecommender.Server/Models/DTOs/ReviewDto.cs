namespace TrackRecommender.Server.Models.DTOs
{
    public class ReviewsResponseDto
    {
        public List<TrailReviewDto> Reviews { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class ReviewFiltersDto
    {
        public int? Rating { get; set; }
        public bool? HasCompleted { get; set; }
        public string? PerceivedDifficulty { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? TrailId { get; set; }
        public int? UserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}