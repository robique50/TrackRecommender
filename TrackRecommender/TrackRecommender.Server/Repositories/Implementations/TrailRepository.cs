using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class TrailRepository : ITrailRepository
    {
        private readonly AppDbContext _context;

        public TrailRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Trail>> GetAllTrailsAsync()
        {
            return await _context.Trails.ToListAsync();
        }

        public async Task<Trail?> GetTrailByIdAsync(int id)
        {
            return await _context.Trails.FindAsync(id);
        }

        public async Task<List<Trail>> GetTrailsByBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
        {
            var trails = await _context.Trails.ToListAsync();
            var result = new List<Trail>();

            foreach (var trail in trails)
            {
                try
                {
                    var geoJson = JsonDocument.Parse(trail.GeoJsonData);
                    var coordinates = geoJson.RootElement
                        .GetProperty("coordinates")
                        .EnumerateArray()
                        .Select(coord => new
                        {
                            Lng = coord[0].GetDouble(),
                            Lat = coord[1].GetDouble()
                        })
                        .ToList();

                    bool isInBoundingBox = coordinates.Any(coord =>
                        coord.Lat >= minLat && coord.Lat <= maxLat &&
                        coord.Lng >= minLng && coord.Lng <= maxLng);

                    if (isInBoundingBox)
                    {
                        result.Add(trail);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error at parsing GeoJSON for trail {trail.Id}: {ex.Message}");
                }
            }

            return result;
        }

        public async Task<List<Trail>> GetTrailsByRegionAsync(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
                return await _context.Trails.ToListAsync();

            return await _context.Trails
                .Where(t => t.Region.ToLower() == region.ToLower())
                .ToListAsync();
        }

        public async Task<List<Trail>> FilterTrailsAsync(string? region, string? difficulty, double? maxDistance)
        {
            IQueryable<Trail> query = _context.Trails;

            if (!string.IsNullOrWhiteSpace(region))
            {
                query = query.Where(t => t.Region.ToLower() == region.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                query = query.Where(t => t.Difficulty.ToLower() == difficulty.ToLower());
            }

            if (maxDistance.HasValue && maxDistance.Value > 0)
            {
                query = query.Where(t => t.Distance <= maxDistance.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<bool> TrailExistsByNameAsync(string name)
        {
            return await _context.Trails.AnyAsync(t => t.Name == name);
        }

        public async Task AddTrailAsync(Trail trail)
        {
            await _context.Trails.AddAsync(trail);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
