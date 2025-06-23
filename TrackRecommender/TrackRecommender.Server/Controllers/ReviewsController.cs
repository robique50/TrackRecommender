using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReviewsController(ReviewService reviewService) : ControllerBase
    {
        private readonly ReviewService _reviewService = reviewService;

        [HttpGet("my-reviews")]
        public async Task<IActionResult> GetMyReviews()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var reviews = await _reviewService.GetUserReviewsAsync(userId.Value);
                return Ok(reviews);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving reviews" });
            }
        }

        [HttpGet("trail/{trailId}")]
        public async Task<IActionResult> GetTrailReviews(int trailId)
        {
            try
            {
                var reviews = await _reviewService.GetTrailReviewsAsync(trailId);
                return Ok(reviews);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving trail reviews" });
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentReviews([FromQuery] int count = 10)
        {
            try
            {
                if (count <= 0 || count > 50)
                    count = 10;

                var reviews = await _reviewService.GetRecentReviewsAsync(count);
                return Ok(reviews);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving recent reviews" });
            }
        }

        [HttpGet("trail/{trailId}/stats")]
        public async Task<IActionResult> GetTrailRatingStats(int trailId)
        {
            try
            {
                var (averageRating, reviewCount) = await _reviewService.GetTrailRatingStatsAsync(trailId);
                return Ok(new
                {
                    trailId,
                    averageRating,
                    reviewCount
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving trail statistics" });
            }
        }

        [HttpPost("trail/{trailId}")]
        public async Task<IActionResult> CreateReview(int trailId, [FromBody] CreateReviewDto createReviewDto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var review = await _reviewService.CreateReviewAsync(userId.Value, trailId, createReviewDto);
                return CreatedAtAction(
                    nameof(GetUserTrailReview),
                    new { trailId },
                    review);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while creating the review" });
            }
        }

        [HttpGet("trail/{trailId}/my-review")]
        public async Task<IActionResult> GetUserTrailReview(int trailId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var review = await _reviewService.GetUserTrailReviewAsync(userId.Value, trailId);
                if (review == null)
                    return NotFound(new { message = "No review found for this trail" });

                return Ok(review);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the review" });
            }
        }

        [HttpDelete("{reviewId}")]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var success = await _reviewService.DeleteReviewAsync(reviewId, userId.Value);
                if (!success)
                    return NotFound(new { message = "Review not found or you don't have permission to delete it" });

                return Ok(new { message = "Review deleted successfully" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the review" });
            }
        }

        [HttpGet("trail/{trailId}/can-review")]
        public async Task<IActionResult> CanUserReviewTrail(int trailId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var hasReviewed = await _reviewService.HasUserReviewedTrailAsync(userId.Value, trailId);
                return Ok(new
                {
                    canReview = !hasReviewed,
                    hasReviewed,
                    trailId
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while checking review status" });
            }
        }

        [HttpGet("all")]
        [Authorize]
        public async Task<IActionResult> GetAllReviews([FromQuery] ReviewFiltersDto filters)
        {
            try
            {
                if (filters.Page <= 0)
                    filters.Page = 1;

                if (filters.PageSize <= 0 || filters.PageSize > 100)
                    filters.PageSize = 20;

                var response = await _reviewService.GetAllReviewsAsync(filters);
                return Ok(response);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving reviews" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return null;

            if (int.TryParse(userIdClaim.Value, out int userId))
                return userId;

            return null;
        }
    }
}