namespace TrackRecommender.Server.Models.DTOs
{
    public enum ShapeType { Svg, Emoji, Text }

    public class TrailMarkingDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string ForegroundColor { get; set; } = string.Empty; 
        public string BackgroundColor { get; set; } = string.Empty; 
        public string Shape { get; set; } = string.Empty; 
        public string? ShapeColor { get; set; }
        public ShapeType ShapeType { get; set; } = ShapeType.Svg;
        public string DisplayName { get; set; } = string.Empty; 
    }
}
