using Microsoft.AspNetCore.Mvc;
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
    }
}