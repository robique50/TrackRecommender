using TrackRecommender.Server.Mappers.Implementations;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Services
{
    public class ReviewService(
        IReviewRepository reviewRepository,
        ITrailRepository trailRepository,
        IUserRepository userRepository,
        ReviewMapper reviewMapper)
    {
        private readonly IReviewRepository _reviewRepository = reviewRepository;
        private readonly ITrailRepository _trailRepository = trailRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ReviewMapper _reviewMapper = reviewMapper;

        public async Task<List<TrailReviewDto>> GetUserReviewsAsync(int userId)
        {
            var reviews = await _reviewRepository.GetUserReviewsAsync(userId);
            return [.. reviews.Select(_reviewMapper.ToDto)];
        }

        public async Task<List<TrailReviewDto>> GetTrailReviewsAsync(int trailId)
        {
            var reviews = await _reviewRepository.GetTrailReviewsAsync(trailId);
            return [.. reviews.Select(_reviewMapper.ToDto)];
        }

        public async Task<List<TrailReviewDto>> GetRecentReviewsAsync(int count = 10)
        {
            var reviews = await _reviewRepository.GetRecentReviewsAsync(count);
            return [.. reviews.Select(_reviewMapper.ToDto)];
        }

        public async Task<TrailReviewDto?> GetUserTrailReviewAsync(int userId, int trailId)
        {
            var review = await _reviewRepository.GetUserTrailReviewAsync(userId, trailId);
            return review != null ? _reviewMapper.ToDto(review) : null;
        }

        public async Task<TrailReviewDto> CreateReviewAsync(int userId, int trailId, CreateReviewDto createReviewDto)
        {
            _ = await _trailRepository.GetTrailByIdAsync(trailId) ?? throw new ArgumentException($"Trail with ID {trailId} not found");
            _ = await _userRepository.GetUserByIdAsync(userId) ?? throw new ArgumentException($"User with ID {userId} not found");
            var existingReview = await _reviewRepository.GetUserTrailReviewAsync(userId, trailId);
            if (existingReview != null)
                throw new InvalidOperationException("You have already reviewed this trail");

            if (createReviewDto.HasCompleted && createReviewDto.CompletedAt.HasValue)
            {
                if (createReviewDto.CompletedAt.Value > DateTime.UtcNow)
                    throw new ArgumentException("Completion date cannot be in the future");
                var minAllowedDate = DateTime.UtcNow.AddYears(-5);
                if (createReviewDto.CompletedAt.Value < minAllowedDate)
                    throw new ArgumentException("Completion date cannot be older than 5 years");
            }

            var review = _reviewMapper.MapCreateDtoToEntity(createReviewDto, userId, trailId);
            _ = await _reviewRepository.CreateReviewAsync(review);
            await _reviewRepository.SaveChangesAsync();

            var fullReview = await _reviewRepository.GetUserTrailReviewAsync(userId, trailId);
            return _reviewMapper.ToDto(fullReview!);
        }

        public async Task<bool> DeleteReviewAsync(int reviewId, int userId)
        {
            var success = await _reviewRepository.DeleteReviewAsync(reviewId, userId);
            if (success)
            {
                await _reviewRepository.SaveChangesAsync();
            }
            return success;
        }

        public async Task<(double averageRating, int reviewCount)> GetTrailRatingStatsAsync(int trailId)
        {
            var averageRating = await _reviewRepository.GetTrailAverageRatingAsync(trailId);
            var reviewCount = await _reviewRepository.GetTrailReviewCountAsync(trailId);

            return (Math.Round(averageRating, 1), reviewCount);
        }

        public async Task<bool> HasUserReviewedTrailAsync(int userId, int trailId)
        {
            var review = await _reviewRepository.GetUserTrailReviewAsync(userId, trailId);
            return review != null;
        }

        public async Task<ReviewsResponseDto> GetAllReviewsAsync(ReviewFiltersDto filters)
        {
            var (reviews, totalCount) = await _reviewRepository.GetAllReviewsAsync(
                filters.Rating,
                filters.HasCompleted,
                filters.PerceivedDifficulty,
                filters.StartDate,
                filters.EndDate,
                filters.TrailId,
                filters.UserId,
                filters.Page,
                filters.PageSize
            );

            return new ReviewsResponseDto
            {
                Reviews = reviews.Select(_reviewMapper.ToDto).ToList(),
                TotalCount = totalCount,
                Page = filters.Page,
                PageSize = filters.PageSize
            };
        }
    }
}