namespace TrackRecommender.Server.Models.DTOs
{
    public class TrailRecommendationDto
    {
        public TrailDto Trail { get; set; } = null!;
        public double RecommendationScore { get; set; }
        public Dictionary<string, double> ScoreBreakdown { get; set; } = [];
        public List<string> MatchReasons { get; set; } = [];
    }

    public class RecommendationRequestDto
    {
        public int Count { get; set; } = 10;
        public bool IncludeWeather { get; set; } = true;
        public List<int>? ExcludeTrailIds { get; set; }
    }

    public class RecommendationResponseDto
    {
        public List<TrailRecommendationDto> Recommendations { get; set; } = [];
        public int TotalCount { get; set; }
        public DateTime GeneratedAt { get; set; }
        public bool WeatherDataAvailable { get; set; }
    }
}