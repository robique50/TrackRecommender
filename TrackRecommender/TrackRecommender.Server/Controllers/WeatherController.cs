using Microsoft.AspNetCore.Mvc;
using TrackRecommender.Server.Services.Implementations;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weatherService;

        public WeatherController(WeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet("coordinates")]
        public async Task<IActionResult> GetWeatherByCoordinates([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var weather = await _weatherService.GetWeatherByCoordinatesAsync(latitude, longitude);
            if (weather == null)
            {
                return StatusCode(500, new { message = "An error occurred while fetching weather data" });
            }
            return Ok(weather);
        }

        [HttpGet("location")]
        public async Task<IActionResult> GetWeatherByLocation([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Location query is required" });
            }

            try
            {
                var weather = await _weatherService.GetWeatherByLocationNameAsync(query);
                if (weather == null)
                {
                    return StatusCode(500, new { message = "An error occurred while fetching weather data" });
                }
                return Ok(weather);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}