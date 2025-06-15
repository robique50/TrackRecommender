using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserPreferencesController(
        UserPreferencesService preferencesService,
        ILogger<UserPreferencesController> logger) : ControllerBase
    {
        private readonly UserPreferencesService _preferencesService = preferencesService;
        private readonly ILogger<UserPreferencesController> _logger = logger;

        [HttpGet]
        public async Task<IActionResult> GetUserPreferences()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var preferences = await _preferencesService.GetUserPreferencesAsync(userId.Value);
                if (preferences == null)
                {
                    return Ok(new UserPreferencesDto());
                }
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user preferences for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving preferences" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveUserPreferences([FromBody] UserPreferencesDto preferencesDto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _preferencesService.SaveUserPreferencesAsync(userId.Value, preferencesDto);
                return Ok(new { message = "Preferences saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user preferences for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while saving preferences" });
            }
        }

        [HttpGet("options")]
        public async Task<IActionResult> GetPreferenceOptions()
        {
            try
            {
                var options = await _preferencesService.GetPreferenceOptionsAsync();
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving preference options");
                return StatusCode(500, new { message = "An error occurred while retrieving preference options" });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> ResetUserPreferences()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                await _preferencesService.ResetUserPreferencesAsync(userId.Value);
                return Ok(new { message = "Preferences reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user preferences for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while resetting preferences" });
            }
        }

        [HttpGet("markings")]
        public async Task<IActionResult> GetMarkingPreferences()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var preferences = await _preferencesService.GetMarkingPreferencesAsync(userId.Value);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving marking preferences for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving marking preferences" });
            }
        }

        [HttpPost("markings")]
        public async Task<IActionResult> SaveMarkingPreferences([FromBody] List<TrailMarkingDto> preferences)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                await _preferencesService.SaveMarkingPreferencesAsync(userId.Value, preferences);
                return Ok(new { message = "Marking preferences saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving marking preferences for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while saving marking preferences" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return null;

            if (int.TryParse(userIdClaim.Value, out int userId))
                return userId;

            return null;
        }
    }
}