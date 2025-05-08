using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Repositories.Interfaces
{
    public interface IRegionRepository
    {
        Task<List<Region>> GetAllRegionsAsync();
        Task<Region?> GetRegionByIdAsync(int id);
        Task<Region?> GetRegionByNameAsync(string name);
        Task AddRegionAsync(Region region);
        Task SaveChangesAsync();
    }
}
