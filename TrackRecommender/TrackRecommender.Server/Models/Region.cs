
namespace TrackRecommender.Server.Models
{
    public class Region
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<Trail> Trails { get; set; } = new List<Trail>();

        public Region(int id, string name)
        {
            Id = id;
            Name = name;
            Trails = new List<Trail>();
        }

        public Region()
        {
            Name = string.Empty;
            Trails = new List<Trail>();
        }
    }
}
