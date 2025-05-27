using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Roles = "Admin")]
    public class RegionImportController : ControllerBase
    {
        private readonly RegionImportService _regionImportService;
        private readonly ILogger<RegionImportController> _logger;

        public RegionImportController(
            RegionImportService regionImportService,
            TrailImportService trailImportService,
            ILogger<RegionImportController> logger)
        {
            _regionImportService = regionImportService;
            _logger = logger;
        }

        [HttpPost("import-regions")]
        public async Task<IActionResult> ImportRomanianCounties()
        {
            try
            {
                _logger.LogInformation("Starting Romanian counties import via API");

                var importedRegions = await _regionImportService.ImportRomanianCountiesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Successfully imported {importedRegions.Count} counties",
                    counties = importedRegions.Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        hasValidGeometry = r.Boundary?.IsValid ?? false
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Romanian counties");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while importing counties",
                    error = ex.Message
                });
            }
        }
    }
}