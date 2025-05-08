
namespace TrackRecommender.Server.Models
{
    public class Region
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLon { get; set; }
        public double MaxLon { get; set; }
        public ICollection<Trail> Trails { get; set; } = new List<Trail>();

        public Region(int id, string name, string description,
                     double minLat, double maxLat, double minLon, double maxLon)
        {
            Id = id;
            Name = name;
            Description = description;
            MinLat = minLat;
            MaxLat = maxLat;
            MinLon = minLon;
            MaxLon = maxLon;
        }
        public string GetBoundingBoxString()
        {
            return $"{MinLat},{MinLon},{MaxLat},{MaxLon}";
        }

        public string ToLower()
        {
            return Name.ToLower();
        }
    }
}
