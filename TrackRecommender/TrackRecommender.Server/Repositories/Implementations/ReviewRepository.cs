using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class ReviewRepository(AppDbContext context) : IReviewRepository
    {
        private readonly AppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<List<UserTrailRating>> GetUserReviewsAsync(int userId)
        {
            return await _context.UserTrailRatings
                .Include(r => r.Trail)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RatedAt)
                .ToListAsync();
        }

        public async Task<List<UserTrailRating>> GetTrailReviewsAsync(int trailId)
        {
            return await _context.UserTrailRatings
                .Include(r => r.User)
                .Where(r => r.TrailId == trailId)
                .OrderByDescending(r => r.RatedAt)
                .ToListAsync();
        }

        public async Task<List<UserTrailRating>> GetRecentReviewsAsync(int count = 10)
        {
            return await _context.UserTrailRatings
                .Include(r => r.User)
                .Include(r => r.Trail)
                .OrderByDescending(r => r.RatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<(List<UserTrailRating> reviews, int totalCount)> GetAllReviewsAsync(
            int? rating = null,
            bool? hasCompleted = null,
            string? perceivedDifficulty = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? trailId = null,
            int? userId = null,
            int page = 1,
            int pageSize = 20)
        {
            var query = _context.UserTrailRatings
                .Include(r => r.User)
                .Include(r => r.Trail)
                .AsQueryable();

            if (rating.HasValue)
                query = query.Where(r => r.Rating == rating.Value);

            if (hasCompleted.HasValue)
                query = query.Where(r => r.HasCompleted == hasCompleted.Value);

            if (!string.IsNullOrWhiteSpace(perceivedDifficulty))
                query = query.Where(r => r.PerceivedDifficulty == perceivedDifficulty);

            if (startDate.HasValue)
                query = query.Where(r => r.RatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(r => r.RatedAt <= endDate.Value);

            if (trailId.HasValue)
                query = query.Where(r => r.TrailId == trailId.Value);

            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);

            var totalCount = await query.CountAsync();

            var reviews = await query
                .OrderByDescending(r => r.RatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (reviews, totalCount);
        }

        public async Task<UserTrailRating?> GetUserTrailReviewAsync(int userId, int trailId)
        {
            return await _context.UserTrailRatings
                .Include(r => r.Trail)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.TrailId == trailId);
        }

        public async Task<UserTrailRating> CreateReviewAsync(UserTrailRating review)
        {
            ArgumentNullException.ThrowIfNull(review);

            var existingReview = await GetUserTrailReviewAsync(review.UserId, review.TrailId);
            if (existingReview != null)
                throw new InvalidOperationException("User has already reviewed this trail.");

            review.RatedAt = DateTime.UtcNow;
            await _context.UserTrailRatings.AddAsync(review);
            return review;
        }

        public async Task<bool> DeleteReviewAsync(int reviewId, int userId)
        {
            var review = await _context.UserTrailRatings
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
                return false;

            _context.UserTrailRatings.Remove(review);
            return true;
        }

        public async Task<double> GetTrailAverageRatingAsync(int trailId)
        {
            var ratings = await _context.UserTrailRatings
                .Where(r => r.TrailId == trailId)
                .Select(r => r.Rating)
                .ToListAsync();

            return ratings.Count != 0 ? ratings.Average() : 0.0;
        }

        public async Task<int> GetTrailReviewCountAsync(int trailId)
        {
            return await _context.UserTrailRatings
                .CountAsync(r => r.TrailId == trailId);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}