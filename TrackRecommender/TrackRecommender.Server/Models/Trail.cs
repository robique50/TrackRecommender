using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrackRecommender.Server.Models
{
    public class Trail
    {
        [Key]
        public int Id { get; set; }
        public long OsmId { get; set; }
        [Required]
        [MaxLength(255)]
        public string Name { get; set; }
        public string Description { get; set; }
        public double Distance { get; set; }
        public double Duration { get; set; }
        [MaxLength(50)]
        public string Difficulty { get; set; }
        [MaxLength(50)]
        public string TrailType { get; set; }
        [MaxLength(100)]
        public string StartLocation { get; set; }
        [MaxLength(100)]
        public string EndLocation { get; set; }
        [Required]
        [Column(TypeName = "geometry")]
        public Geometry Coordinates { get; set; }
        public List<string> Tags { get; set; }
        [MaxLength(50)]
        public string Category { get; set; }
        [MaxLength(50)] 
        public string? Network { get; set; }
        public DateTime LastUpdated { get; set; }
        public virtual ICollection<TrailRegion> TrailRegions { get; set; }
        public virtual ICollection<UserTrailRating> UserRatings { get; set; }
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
            Category = "Local";
            Coordinates = new Point(0, 0) { SRID = 4326 };
            Network = string.Empty; 
            Tags = []; 
            RegionIds = [];
            RegionNames = [];
            TrailRegions = []; 
            UserRatings = []; 
            LastUpdated = DateTime.UtcNow; 
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
            Geometry coordinates,
            List<string> tags,
            string category = "Local",
            string? network = null,
            ICollection<TrailRegion>? trailRegions = null,
            long osmId = 0)
            : this() 
        {
            Name = name;
            OsmId = osmId; 
            Description = description;
            Distance = distance;
            Duration = duration;
            Difficulty = difficulty;
            TrailType = trailType;
            StartLocation = startLocation;
            EndLocation = endLocation;
            Coordinates = coordinates;
            Tags = tags ?? [];
            Category = category;
            Network = network;
            TrailRegions = trailRegions ?? [];
        }

        public static string DetermineCategoryFromNetwork(string? network)
        {
            if (string.IsNullOrEmpty(network))
                return "Local";

            return network.ToLowerInvariant() switch
            {
                string n when n.Contains("iwn") || n.Contains("icn") => "International",
                string n when n.Contains("nwn") || n.Contains("ncn") => "National",
                string n when n.Contains("rwn") || n.Contains("rcn") => "Regional",
                string n when n.Contains("lwn") || n.Contains("lcn") => "Local",
                _ => "Local"
            };
        }
    }
}