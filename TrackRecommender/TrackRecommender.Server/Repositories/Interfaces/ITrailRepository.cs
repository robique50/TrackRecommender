using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Repositories.Interfaces
{
    public interface ITrailRepository
    {
        Task<List<Trail>> GetAllTrailsAsync();
        Task<Trail?> GetTrailByIdAsync(int id);
        Task<List<Trail>> GetTrailsByNameAsync(string name);
        Task<int> GetTrailCountAsync();
        Task<bool> TrailExistsByNameAsync(string name);
        Task AddTrailAsync(Trail trail);
        Task<List<Trail>> GetTrailsByRegionIdAsync(int regionId);
        Task<List<Trail>> GetTrailsByRegionIdsAsync(List<int> regionIds);
        Task AddTrailRegionAsync(int trailId, int regionId);
        Task RemoveTrailRegionAsync(int trailId, int regionId);
        Task<List<int>> GetRegionIdsForTrailAsync(int trailId);
        Task<List<Trail>> FilterTrailsAsync(
            List<int>? regionIds = null,
            string? difficulty = null,
            double? maxDistance = null,
            string? category = null,
            string? trailType = null,
            double? maxDuration = null,
            List<string>? tags = null);
        Task<bool> TrailExistsAsync(string name, string? network);
        Task AddTrailsAsync(IEnumerable<Trail> trails); 
        Task UpdateTrailAsync(Trail trail);
        Task DeleteTrailAsync(int trailId);
        Task<bool> TrailExistsByOsmIdAsync(long osmId);
        Task<Trail?> GetTrailByOsmIdAsync(long osmId);
        Task<List<string>> GetDistinctTrailTypesAsync();
        Task<int> SaveChangesAsync();
    }
}