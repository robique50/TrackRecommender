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
    public class TrailRecommendationController(
        TrailRecommendationService recommendationService,
        ILogger<TrailRecommendationController> logger) : ControllerBase
    {
        private readonly TrailRecommendationService _recommendationService = recommendationService;
        private readonly ILogger<TrailRecommendationController> _logger = logger;

        [HttpGet]
        public async Task<ActionResult<RecommendationResponseDto>> GetRecommendations(
            [FromQuery] int count = 10,
            [FromQuery] bool includeWeather = true)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                if (count < 1 || count > 50)
                {
                    return BadRequest(new { message = "Count must be between 1 and 50" });
                }

                var recommendations = await _recommendationService.GetRecommendationsAsync(userId, count);

                var response = new RecommendationResponseDto
                {
                    Recommendations = recommendations,
                    TotalCount = recommendations.Count,
                    GeneratedAt = DateTime.UtcNow,
                    WeatherDataAvailable = includeWeather
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trail recommendations for user");
                return StatusCode(500, new { message = "An error occurred while generating recommendations" });
            }
        }

    }
}