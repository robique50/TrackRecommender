using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrailImportController : ControllerBase
    {
        private readonly TrailImportService _importService;

        public TrailImportController(TrailImportService importService)
        {
            _importService = importService;
        }

        [HttpPost("import-all-regions")]
        public async Task<IActionResult> ImportAllRegions()
        {
            await _importService.ImportAllTrailsAsync();
            return Ok("Import for all regions finished");
        }
    }
}
