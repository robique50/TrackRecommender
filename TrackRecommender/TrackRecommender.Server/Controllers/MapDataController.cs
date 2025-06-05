using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MapDataController(
        ITrailRepository trailRepository,
        IUserRepository userRepository,
        IMapper<Trail, TrailDto> trailMapper,
        ILogger<MapDataController> logger) : ControllerBase
    {
        private readonly ITrailRepository _trailRepository = trailRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IMapper<Trail, TrailDto> _trailMapper = trailMapper;
        private readonly ILogger<MapDataController> _logger = logger;

        [HttpGet("trails")]
        public async Task<IActionResult> GetTrails(
            [FromQuery] string? difficulty = null,
            [FromQuery] List<int>? regionIds = null,
            [FromQuery] string? trailType = null,
            [FromQuery] double? maxDistance = null,
            [FromQuery] string? category = null,
            [FromQuery] double? maxDuration = null,
            [FromQuery] List<string>? tags = null)
        {
            try
            {
                var trails = await _trailRepository.FilterTrailsAsync(
                    regionIds, difficulty, maxDistance, category, trailType, maxDuration, tags);

                var trailDtos = trails.Select(_trailMapper.ToDto).ToList();

                return Ok(trailDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving trails", error = ex.Message });
            }
        }

        [HttpGet("trails/{id}")]
        public async Task<IActionResult> GetTrail(int id)
        {
            try
            {
                var trail = await _trailRepository.GetTrailByIdAsync(id);
                if (trail == null)
                    return NotFound(new { message = $"Trail with ID {id} not found" });

                var trailDto = _trailMapper.ToDto(trail);
                return Ok(trailDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the trail", error = ex.Message });
            }
        }

        [HttpGet("recommended")]
        [Authorize]
        public async Task<IActionResult> GetRecommendedTrails()
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
                    return Unauthorized(new { message = "User not authenticated or invalid user ID" });

                var preferences = await _userRepository.GetUserPreferencesAsync(userId);
                if (preferences == null)
                    return Ok(new List<TrailDto>()); 

                var trails = await _trailRepository.FilterTrailsAsync(
                    preferences.PreferredRegionIds,
                    preferences.PreferredDifficulty,
                    preferences.MaxDistance,
                    null, 
                    null, 
                    preferences.MaxDuration,
                    preferences.PreferredTags);

                if (preferences.PreferredTrailTypes != null && preferences.PreferredTrailTypes.Count > 0)
                {
                    trails = [.. trails.Where(t =>
                        preferences.PreferredTrailTypes.Any(type =>
                            t.TrailType.Contains(type, StringComparison.OrdinalIgnoreCase)))];
                }

                var trailDtos = trails.Select(t =>
                {
                    var dto = _trailMapper.ToDto(t);
                    dto.MatchScore = CalculateMatchScore(t, preferences);
                    return dto;
                })
                .OrderByDescending(t => t.MatchScore)
                .ToList();

                return Ok(trailDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving recommendations", error = ex.Message });
            }
        }

        private static int CalculateMatchScore(Trail trail, UserPreferences preferences)
        {
            int score = 0;

            if (preferences.PreferredDifficulty == trail.Difficulty)
                score += 2;

            if (preferences.PreferredTrailTypes?.Any(type =>
                trail.TrailType.Contains(type, StringComparison.OrdinalIgnoreCase)) == true)
                score += 2;

            if (trail.Distance <= preferences.MaxDistance)
                score += 1;

            if (trail.Duration <= preferences.MaxDuration)
                score += 1;

            if (preferences.PreferredRegionIds?.Any(id =>
                trail.RegionIds.Contains(id)) == true)
                score += 2;

            if (preferences.PreferredTags != null && preferences.PreferredTags.Count > 0 && trail.Tags.Count != 0)
            {
                bool hasMatchingTag = trail.Tags.Any(tag =>
                    preferences.PreferredTags.Any(prefTag =>
                        tag.Contains(prefTag, StringComparison.OrdinalIgnoreCase)));

                if (hasMatchingTag)
                    score += 1;
            }

            if (preferences.PreferredCategories?.Contains(trail.Category) == true)
                score += 1;

            return score;
        }
    }
}