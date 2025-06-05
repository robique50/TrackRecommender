using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrailImportController(
        TrailImportService importService,
        AppDbContext context,
        ILogger<TrailImportController> logger) : ControllerBase
    {
        private readonly TrailImportService _importService = importService;
        private readonly AppDbContext _context = context;
        private readonly ILogger<TrailImportController> _logger = logger;

        [HttpPost("import-all-trails")]
        public async Task<IActionResult> ImportTrails(CancellationToken cancellationToken)
        {
            try
            {
                var countBefore = await _context.Trails.CountAsync(cancellationToken);

                await _importService.ImportAllTrailsAsync(cancellationToken);

                var countAfter = await _context.Trails.CountAsync(cancellationToken);

                var stats = await GetImportStatistics();

                return Ok(new
                {
                    success = true,
                    message = "Import completed successfully",
                    trailsBefore = countBefore,
                    trailsAfter = countAfter,
                    newTrails = countAfter - countBefore,
                    statistics = stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred during import",
                    error = ex.Message
                });
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var stats = await GetImportStatistics();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("verify-geometry")]
        public async Task<IActionResult> VerifyGeometry([FromQuery] int sampleSize = 10)
        {
            try
            {
                var trails = await _context.Trails
                    .OrderByDescending(t => t.LastUpdated)
                    .Take(sampleSize)
                    .Select(t => new
                    {
                        t.Id,
                        t.OsmId,
                        t.Name,
                        t.Distance,
                        t.Coordinates.GeometryType,
                        PointCount = t.Coordinates.NumPoints,
                        t.Coordinates.IsValid,
                        t.LastUpdated
                    })
                    .ToListAsync();

                return Ok(trails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying geometry");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("remove-duplicates")]
        public async Task<IActionResult> RemoveDuplicates()
        {
            try
            {
                var duplicates = await _context.Trails
                    .GroupBy(t => t.OsmId)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { OsmId = g.Key, Count = g.Count() })
                    .ToListAsync();

                int removedCount = 0;

                foreach (var dup in duplicates)
                {
                    var trails = await _context.Trails
                        .Where(t => t.OsmId == dup.OsmId)
                        .OrderByDescending(t => t.LastUpdated)
                        .ToListAsync();

                    for (int i = 1; i < trails.Count; i++)
                    {
                        _context.Trails.Remove(trails[i]);
                        removedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    duplicatesFound = duplicates.Count,
                    trailsRemoved = removedCount,
                    message = $"Removed {removedCount} duplicate trails"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<object> GetImportStatistics()
        {
            var totalTrails = await _context.Trails.CountAsync();

            var byCategory = await _context.Trails
                .GroupBy(t => t.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var byType = await _context.Trails
                .GroupBy(t => t.TrailType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var byDifficulty = await _context.Trails
                .GroupBy(t => t.Difficulty)
                .Select(g => new { Difficulty = g.Key, Count = g.Count() })
                .ToListAsync();

            var distanceStats = await _context.Trails
                .GroupBy(t => 1)
                .Select(g => new
                {
                    MinDistance = g.Min(t => t.Distance),
                    MaxDistance = g.Max(t => t.Distance),
                    AvgDistance = g.Average(t => t.Distance),
                    TotalDistance = g.Sum(t => t.Distance)
                })
                .FirstOrDefaultAsync();

            var geometryStats = await _context.Trails
                .Select(t => new {
                    GeomType = t.Coordinates.GeometryType,
                    Points = t.Coordinates.NumPoints
                })
                .ToListAsync();

            var avgPointsPerTrail = geometryStats.Count != 0
                ? geometryStats.Average(g => g.Points)
                : 0;

            var geometryTypes = geometryStats
                .GroupBy(g => g.GeomType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToList();

            var recentImports = await _context.Trails
                .OrderByDescending(t => t.LastUpdated)
                .Take(5)
                .Select(t => new { t.Name, t.LastUpdated })
                .ToListAsync();

            return new
            {
                totalTrails,
                byCategory,
                byType,
                byDifficulty,
                distanceStats,
                geometryStats = new
                {
                    averagePointsPerTrail = Math.Round(avgPointsPerTrail, 0),
                    geometryTypes
                },
                recentImports
            };
        }
    }
}