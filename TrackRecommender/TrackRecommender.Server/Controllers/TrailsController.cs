using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrailsController : ControllerBase
    {
        private readonly ITrailRepository _trailRepository;

        public TrailsController(ITrailRepository trailRepository)
        {
            _trailRepository = trailRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Trail>>> GetTrails()
        {
            return Ok(await _trailRepository.GetAllTrailsAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Trail>> GetTrail(int id)
        {
            var trail = await _trailRepository.GetTrailByIdAsync(id);
            if (trail == null)
                return NotFound();

            return Ok(trail);
        }


        //[HttpGet("filter")]
        //public async Task<ActionResult<IEnumerable<Trail>>> FilterTrails(
        //    [FromQuery] int? regionId = null,
        //    [FromQuery] string? difficulty = null,
        //    [FromQuery] double? maxDistance = null)
        //{
        //    return Ok(await _trailRepository.FilterTrailsAsync(regionId, difficulty, maxDistance));
        //}
    }
}
