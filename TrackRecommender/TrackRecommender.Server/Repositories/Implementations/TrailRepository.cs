using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class TrailRepository : ITrailRepository
    {
        private readonly AppDbContext _context;

        public TrailRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Trail>> GetAllTrailsAsync()
        {
            return await _context.Trails
                .Include(t => t.Regions)
                .ToListAsync();
        }

        public async Task<Trail?> GetTrailByIdAsync(int id)
        {
            var trail = await _context.Trails
                .Include(t => t.Regions)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trail != null)
            {
                trail.RegionIds = trail.Regions.Select(r => r.Id).ToList();
                trail.RegionNames = trail.Regions.Select(r => r.Name).ToList();
            }

            return trail;
        }

        public async Task<List<Trail>> GetTrailsByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new List<Trail>();

            var trails = await _context.Trails
                .Where(t => t.Name == name)
                .Include(t => t.Regions)
                .ToListAsync();

            foreach (var trail in trails)
            {
                trail.RegionIds = trail.Regions.Select(r => r.Id).ToList();
                trail.RegionNames = trail.Regions.Select(r => r.Name).ToList();
            }

            return trails;
        }

        public async Task<bool> TrailExistsByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return await _context.Trails.AnyAsync(t => t.Name == name);
        }

        public async Task AddTrailAsync(Trail trail)
        {
            if (trail == null)
                throw new ArgumentNullException(nameof(trail));

            if (trail.RegionIds != null && trail.RegionIds.Any())
            {
                foreach (var regionId in trail.RegionIds)
                {
                    var region = await _context.Regions.FindAsync(regionId);
                    if (region == null)
                    {
                        throw new InvalidOperationException($"Region with ID {regionId} does not exist.");
                    }
                    trail.Regions.Add(region);
                }
            }

            await _context.Trails.AddAsync(trail);
        }

        public async Task<List<Trail>> GetTrailsByRegionIdAsync(int regionId)
        {
            var trails = await _context.Trails
                .Where(t => t.Regions.Any(r => r.Id == regionId))
                .Include(t => t.Regions)
                .ToListAsync();

            foreach (var trail in trails)
            {
                trail.RegionIds = trail.Regions.Select(r => r.Id).ToList();
                trail.RegionNames = trail.Regions.Select(r => r.Name).ToList();
            }

            return trails;
        }

        public async Task<List<Trail>> GetTrailsByRegionIdsAsync(List<int> regionIds)
        {
            if (regionIds == null || !regionIds.Any())
                return await GetAllTrailsAsync();

            var trails = await _context.Trails
                .Where(t => t.Regions.Any(r => regionIds.Contains(r.Id)))
                .Include(t => t.Regions)
                .ToListAsync();

            foreach (var trail in trails)
            {
                trail.RegionIds = trail.Regions.Select(r => r.Id).ToList();
                trail.RegionNames = trail.Regions.Select(r => r.Name).ToList();
            }

            return trails;
        }

        public async Task AddTrailRegionAsync(int trailId, int regionId)
        {
            var trail = await _context.Trails.FindAsync(trailId);
            if (trail == null)
                throw new InvalidOperationException($"Trail with ID {trailId} does not exist.");

            var region = await _context.Regions.FindAsync(regionId);
            if (region == null)
                throw new InvalidOperationException($"Region with ID {regionId} does not exist.");

            var exists = await _context.TrailRegions.AnyAsync(tr =>
                tr.TrailId == trailId && tr.RegionId == regionId);

            if (!exists)
            {
                await _context.TrailRegions.AddAsync(new TrailRegion
                {
                    TrailId = trailId,
                    RegionId = regionId
                });
            }
        }

        public async Task RemoveTrailRegionAsync(int trailId, int regionId)
        {
            var trailRegion = await _context.TrailRegions
                .FirstOrDefaultAsync(tr => tr.TrailId == trailId && tr.RegionId == regionId);

            if (trailRegion != null)
            {
                _context.TrailRegions.Remove(trailRegion);
            }
        }

        public async Task<List<int>> GetRegionIdsForTrailAsync(int trailId)
        {
            return await _context.TrailRegions
                .Where(tr => tr.TrailId == trailId)
                .Select(tr => tr.RegionId)
                .ToListAsync();
        }

        public async Task<List<Trail>> FilterTrailsAsync(
    List<int>? regionIds = null,
    string? difficulty = null,
    double? maxDistance = null,
    string? category = null,
    string? trailType = null,
    double? maxDuration = null,
    List<string>? tags = null)
        {
            IQueryable<Trail> query = _context.Trails
                .Include(t => t.Regions)
                .AsNoTracking();

            if (regionIds != null && regionIds.Any())
            {
                query = query.Where(t => t.Regions.Any(r => regionIds.Contains(r.Id)));
            }

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                query = query.Where(t => t.Difficulty == difficulty);
            }

            if (maxDistance.HasValue && maxDistance.Value > 0)
            {
                query = query.Where(t => t.Distance <= maxDistance.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(t => t.Category == category);
            }

            if (maxDuration.HasValue && maxDuration.Value > 0)
            {
                query = query.Where(t => t.Duration <= maxDuration.Value);
            }

            var trails = await query.ToListAsync();

            if (!string.IsNullOrWhiteSpace(trailType))
            {
                trails = trails.Where(t => t.TrailType.Contains(trailType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (tags != null && tags.Any())
            {
                trails = trails.Where(t =>
                    t.Tags.Any(tag => tags.Any(searchTag =>
                        tag.Contains(searchTag, StringComparison.OrdinalIgnoreCase)))
                ).ToList();
            }

            foreach (var trail in trails)
            {
                trail.RegionIds = trail.Regions.Select(r => r.Id).ToList();
                trail.RegionNames = trail.Regions.Select(r => r.Name).ToList();
            }

            return trails;
        }

        public async Task<bool> TrailExistsAsync(string name, string network, string geoJsonData)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(geoJsonData))
                return false;

            return await _context.Trails.AnyAsync(t =>
                t.Name == name &&
                t.Network == network &&
                t.GeoJsonData == geoJsonData);
        }

        public async Task UpdateTrailAsync(Trail trail)
        {
            if (trail == null)
                throw new ArgumentNullException(nameof(trail));

            if (trail.RegionIds != null && trail.RegionIds.Any())
            {
                var currentRegionIds = await GetRegionIdsForTrailAsync(trail.Id);

                var regionsToAdd = trail.RegionIds.Except(currentRegionIds);
                foreach (var regionId in regionsToAdd)
                {
                    await AddTrailRegionAsync(trail.Id, regionId);
                }

                var regionsToRemove = currentRegionIds.Except(trail.RegionIds);
                foreach (var regionId in regionsToRemove)
                {
                    await RemoveTrailRegionAsync(trail.Id, regionId);
                }
            }

            _context.Trails.Update(trail);
        }

        public async Task DeleteTrailAsync(int trailId)
        {
            var trail = await _context.Trails.FindAsync(trailId);
            if (trail != null)
            {
                var trailRegions = await _context.TrailRegions
                    .Where(tr => tr.TrailId == trailId)
                    .ToListAsync();

                _context.TrailRegions.RemoveRange(trailRegions);

                _context.Trails.Remove(trail);
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}