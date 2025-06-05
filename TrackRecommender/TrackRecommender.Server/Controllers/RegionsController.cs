using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegionsController(RegionService regionService) : ControllerBase
    {
        private readonly RegionService _regionService = regionService ?? throw new ArgumentNullException(nameof(regionService));

        [HttpGet]
        public async Task<IActionResult> GetAllRegions()
        {
            try
            {
                var regions = await _regionService.GetAllRegionsAsync();
                return Ok(regions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving regions", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRegionById(int id)
        {
            try
            {
                var region = await _regionService.GetRegionByIdAsync(id);
                if (region == null)
                    return NotFound(new { message = $"Region with ID {id} not found" });

                return Ok(region);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the region", error = ex.Message });
            }
        }

        [HttpGet("{id}/trails")]
        public async Task<IActionResult> GetTrailsByRegionId(int id)
        {
            try
            {
                var trails = await _regionService.GetTrailsByRegionIdAsync(id);
                return Ok(trails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving trails for the region", error = ex.Message });
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetRegionStatistics()
        {
            try
            {
                var regions = await _regionService.GetAllRegionsAsync();
                var statistics = new
                {
                    TotalRegions = regions.Count,
                    RegionsWithTrails = regions.Count(r => r.TrailCount > 0),
                    TotalTrails = regions.Sum(r => r.TrailCount),
                    TopRegions = regions
                        .OrderByDescending(r => r.TrailCount)
                        .Take(10)
                        .Select(r => new { r.Name, r.TrailCount })
                        .ToList()
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving statistics", error = ex.Message });
            }
        }
    }
}