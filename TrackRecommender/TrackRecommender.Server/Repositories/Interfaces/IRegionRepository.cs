using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Repositories.Interfaces
{
    public interface IRegionRepository
    {
        Task<List<Region>> GetAllRegionsAsync(bool includeTrails = false);
        Task<Region?> GetRegionByIdAsync(int id, bool includeTrails = false);
        Task<Region?> GetRegionByNameAsync(string name, bool includeTrails = false);
        Task AddRegionAsync(Region region);
        Task<List<Region>> GetRegionsByTrailIdAsync(int trailId);
        Task<List<string>> GetRegionNamesByTrailIdAsync(int trailId);
        Task<List<Trail>> GetTrailsByRegionIdAsync(int regionId);

Task<int> GetTrailCountByRegionIdAsync(int regionId);
        Task UpdateRegion(Region region);
        Task DeleteRegionAsync(int regionId);
        Task<int> SaveChangesAsync();
    }
}