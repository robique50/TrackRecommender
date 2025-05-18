using System.ComponentModel.DataAnnotations.Schema;

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
        public string Category { get; set; } 
        public string? Network { get; set; } 
        public virtual ICollection<Region> Regions { get; set; }
        public virtual ICollection<UserTrailRating> UserRatings { get; set; } = new List<UserTrailRating>();

        [NotMapped]
        public List<int> RegionIds { get; set; }

        [NotMapped]
        public List<string> RegionNames { get; set; }

        public Trail()
        {
            Name = string.Empty;
            Description = string.Empty;
            Difficulty = string.Empty;
            TrailType = string.Empty;
            StartLocation = string.Empty;
            EndLocation = string.Empty;
            GeoJsonData = string.Empty;
            Category = "Local";
            Network = string.Empty;
            Tags = new List<string>();
            Regions = new List<Region>();
            RegionIds = new List<int>();
            RegionNames = new List<string>();
        }

        public Trail(
            string name,
            string description,
            double distance,
            double duration,
            string difficulty,
            string trailType,
            string startLocation,
            string endLocation,
            string geoJsonData,
            List<string> tags,
            string category = "Local",
            string network = "",
            ICollection<Region>? regions = null)
            : this()
        {
            Name = name;
            Description = description;
            Distance = distance;
            Duration = duration;
            Difficulty = difficulty;
            TrailType = trailType;
            StartLocation = startLocation;
            EndLocation = endLocation;
            GeoJsonData = geoJsonData;
            Tags = tags ?? new List<string>();
            Category = category;
            Network = network ?? string.Empty;
            Regions = regions ?? new List<Region>();
        }

        public static string DetermineCategoryFromNetwork(string network)
        {
            if (string.IsNullOrEmpty(network))
                return "Local";

            return network.ToLower() switch
            {
                var n when n.Contains("iwn") || n.Contains("icn") => "International",
                var n when n.Contains("nwn") || n.Contains("ncn") => "National",
                var n when n.Contains("rwn") || n.Contains("rcn") => "Regional",
                var n when n.Contains("lwn") || n.Contains("lcn") => "Local",
                _ => "Local"
            };
        }
    }
}