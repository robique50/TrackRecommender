namespace TrackRecommender.Server.Models
{
    public class Trail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string Difficulty { get; set; }
        public string TrailType { get; set; }
        public string StartLocation { get; set; }
        public string EndLocation { get; set; }
        public string GeoJsonData { get; set; }
        public List<string> Tags { get; set; }
        public int RegionId { get; set; }
        public Region? Region { get; set; }

        public Trail(int id, string name, string description, double distance, double duration, string difficulty,
            string trailType, string startLocation, string endLocation, string geoJsonData, List<string> tags, int regionId)
        {
            Id = id;
            Name = name;
            Description = description;
            Distance = distance;
            Duration = duration;
            Difficulty = difficulty;
            TrailType = trailType;
            StartLocation = startLocation;
            EndLocation = endLocation;
            GeoJsonData = geoJsonData;
            Tags = tags;
            RegionId = regionId;
        }
    }
}
