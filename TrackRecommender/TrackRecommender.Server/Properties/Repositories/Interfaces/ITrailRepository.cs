using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Properties.Repositories.Interfaces
{
    public interface ITrailRepository
    {
        Task<List<Trail>> GetAllTrailsAsync();
        Task<Trail> GetTrailByIdAsync(int id);
        Task<List<Trail>> GetTrailsByBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
        Task<bool> TrailExistsByNameAsync(string name);
        Task AddTrailAsync(Trail trail);
        Task<List<Trail>> GetTrailsByRegionAsync(string region);
        Task<List<Trail>> FilterTrailsAsync(string? region, string? difficulty, double? maxDistance);
        Task SaveChangesAsync();
    }
}
