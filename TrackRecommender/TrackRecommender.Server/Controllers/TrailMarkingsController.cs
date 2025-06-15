using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrailMarkingsController(
        TrailMarkingService markingService,
        ITrailRepository trailRepository,
        ILogger<TrailMarkingsController> logger) : ControllerBase
    {
        private readonly TrailMarkingService _markingService = markingService;
        private readonly ITrailRepository _trailRepository = trailRepository;
        private readonly ILogger<TrailMarkingsController> _logger = logger;

        private static List<TrailMarkingDto>? _allMarkingsCache;
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

        [HttpGet]
        public async Task<IActionResult> GetAllMarkings()
        {
            try
            {
                if (_allMarkingsCache != null &&
                    DateTime.Now - _lastCacheUpdate < _cacheDuration)
                {
                    return Ok(_allMarkingsCache);
                }

                var osmcTags = await _trailRepository.GetUniqueTagsAsync("osmc:symbol=");

                _logger.LogInformation("Found {TagsCount} unique OSMC tags", osmcTags.Count);

                var markings = new List<TrailMarkingDto>();
                foreach (var tag in osmcTags)
                {
                    var marking = _markingService.ParseOsmcSymbol(tag);
                    if (marking != null)
                    {
                        markings.Add(marking);
                    }
                }

                markings = [.. markings.OrderBy(m => m.DisplayName)];

                _allMarkingsCache = markings;
                _lastCacheUpdate = DateTime.Now;

                return Ok(markings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trail markings");
                return StatusCode(500, new { message = "Error retrieving trail markings" });
            }
        }
    }
}