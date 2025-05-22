using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Repositories.Interfaces
{
    public interface IReviewRepository
    {
        Task<List<UserTrailRating>> GetUserReviewsAsync(int userId);
        Task<List<UserTrailRating>> GetTrailReviewsAsync(int trailId);
        Task<List<UserTrailRating>> GetRecentReviewsAsync(int count = 10);
        Task<UserTrailRating?> GetUserTrailReviewAsync(int userId, int trailId);
        Task<UserTrailRating> CreateReviewAsync(UserTrailRating review);
        Task<bool> DeleteReviewAsync(int reviewId, int userId);
        Task<double> GetTrailAverageRatingAsync(int trailId);
        Task<int> GetTrailReviewCountAsync(int trailId);
        Task<int> SaveChangesAsync();
    }
}
