using NetTopologySuite.Geometries;

namespace TrackRecommender.Server.Models
{
    public class Region
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Geometry? Boundary { get; set; }
        public virtual ICollection<TrailRegion> Trails { get; set; } = new List<TrailRegion>();

        public Region(int id, string name)
        {
            Id = id;
            Name = name;
            Trails = new List<TrailRegion>();
        }

        public Region()
        {
            Name = string.Empty;
            Trails = new List<TrailRegion>();
        }
    }
}
