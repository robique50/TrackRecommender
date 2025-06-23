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
        Task<(List<UserTrailRating> reviews, int totalCount)> GetAllReviewsAsync(
        int? rating = null,
        bool? hasCompleted = null,
        string? perceivedDifficulty = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? trailId = null,
        int? userId = null,
        int page = 1,
        int pageSize = 20);
        Task<int> SaveChangesAsync();
    }
}
