using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.DTOs;
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

        [HttpPost("import")]
        public async Task<IActionResult> ImportTrails([FromBody] ImportTrailsRequest request)
        {
            await _importService.ImportTrailsFromOverpassAsync(request.BoundingBox);
            return Ok("Import started");
        }
    }
}
