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