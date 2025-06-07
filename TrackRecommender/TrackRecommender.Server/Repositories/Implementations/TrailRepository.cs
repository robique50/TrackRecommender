using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class TrailRepository(AppDbContext context) : ITrailRepository
    {
        private readonly AppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<List<Trail>> GetAllTrailsAsync()
        {
            return await _context.Trails
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .ToListAsync();
        }

        public async Task<Trail?> GetTrailByIdAsync(int id)
        {
            var trail = await _context.Trails
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trail?.TrailRegions != null)
            {
                trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                trail.RegionNames = [.. trail.TrailRegions
                                        .Where(tr => tr.Region?.Name != null)
                                        .Select(tr => tr.Region!.Name)];
            }
            return trail;
        }

        public async Task<List<Trail>> GetTrailsByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return [];

            var trails = await _context.Trails
                .Where(t => t.Name.Contains(name))
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .ToListAsync();

            foreach (var trail in trails)
            {
                if (trail.TrailRegions != null)
                {
                    trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                    trail.RegionNames = [.. trail.TrailRegions
                                            .Where(tr => tr.Region?.Name != null)
                                            .Select(tr => tr.Region!.Name)];
                }
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
            ArgumentNullException.ThrowIfNull(trail);

            if (trail.RegionIds != null && trail.RegionIds.Count != 0)
            {
                trail.TrailRegions ??= [];

                foreach (var regionId in trail.RegionIds)
                {
                    if (!trail.TrailRegions.Any(tr => tr.RegionId == regionId))
                    {
                        trail.TrailRegions.Add(new TrailRegion { RegionId = regionId });
                    }
                }
            }
            await _context.Trails.AddAsync(trail);
        }

        public async Task<List<Trail>> GetTrailsByRegionIdAsync(int regionId)
        {
            var trails = await _context.Trails
                .Where(t => t.TrailRegions.Any(tr => tr.RegionId == regionId))
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .ToListAsync();

            foreach (var trail in trails)
            {
                if (trail.TrailRegions != null)
                {
                    trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                    trail.RegionNames = [.. trail.TrailRegions
                                            .Where(tr => tr.Region?.Name != null)
                                            .Select(tr => tr.Region!.Name)];
                }
            }
            return trails;
        }

        public async Task<List<Trail>> GetTrailsByRegionIdsAsync(List<int> regionIds)
        {
            if (regionIds == null || regionIds.Count == 0)
                return await GetAllTrailsAsync();

            var trails = await _context.Trails
                .Where(t => t.TrailRegions.Any(tr => regionIds.Contains(tr.RegionId)))
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .ToListAsync();

            foreach (var trail in trails)
            {
                if (trail.TrailRegions != null)
                {
                    trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                    trail.RegionNames = [.. trail.TrailRegions
                                            .Where(tr => tr.Region?.Name != null)
                                            .Select(tr => tr.Region!.Name)];
                }
            }
            return trails;
        }

        public async Task AddTrailRegionAsync(int trailId, int regionId)
        {
            var trailExists = await _context.Trails.AnyAsync(t => t.Id == trailId);
            if (!trailExists)
                throw new InvalidOperationException($"Trail with ID {trailId} does not exist.");

            var regionExists = await _context.Regions.AnyAsync(r => r.Id == regionId);
            if (!regionExists)
                throw new InvalidOperationException($"Region with ID {regionId} does not exist.");

            var exists = await _context.TrailRegions.AnyAsync(tr =>
                tr.TrailId == trailId && tr.RegionId == regionId);

            if (!exists)
            {
                _context.TrailRegions.Add(new TrailRegion
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
                .Include(t => t.TrailRegions)
                    .ThenInclude(tr => tr.Region)
                .Include(t => t.UserRatings)
                .AsNoTracking();

            if (regionIds != null && regionIds.Count != 0)
            {
                query = query.Where(t => t.TrailRegions.Any(tr => regionIds.Contains(tr.RegionId)));
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

            if (!string.IsNullOrWhiteSpace(trailType))
            {
                query = query.Where(t => t.TrailType != null && t.TrailType.Contains(trailType, StringComparison.OrdinalIgnoreCase));
            }

            if (maxDuration.HasValue && maxDuration.Value > 0)
            {
                query = query.Where(t => t.Duration <= maxDuration.Value);
            }

            var trails = await query.ToListAsync();

            if (tags != null && tags.Count != 0)
            {
                trails = [.. trails.Where(t =>
                    t.Tags != null && t.Tags.Any(tagInEntity => tags.Any(searchTag =>
                        tagInEntity.Contains(searchTag, StringComparison.OrdinalIgnoreCase)))
                )];
            }

            foreach (var trail in trails)
            {
                if (trail.TrailRegions != null)
                {
                    trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                    trail.RegionNames = [.. trail.TrailRegions
                                            .Where(tr => tr.Region?.Name != null)
                                            .Select(tr => tr.Region!.Name)];
                }
            }
            return trails;
        }

        public async Task<bool> TrailExistsAsync(string name, string? network)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var query = _context.Trails.Where(t => t.Name == name);

            if (!string.IsNullOrEmpty(network))
            {
                query = query.Where(t => t.Network == network);
            }
            return await query.AnyAsync();
        }

        public async Task UpdateTrailAsync(Trail trail)
        {
            ArgumentNullException.ThrowIfNull(trail);

            var existingTrail = await _context.Trails
                .Include(t => t.TrailRegions)
                .FirstOrDefaultAsync(t => t.Id == trail.Id) ?? throw new KeyNotFoundException($"Trail with ID {trail.Id} not found for update.");
            _context.Entry(existingTrail).CurrentValues.SetValues(trail);

            if (trail.TrailRegions != null)
            {
                var regionIdsInNewList = trail.TrailRegions.Select(tr => tr.RegionId).ToList();
                var regionsToRemove = existingTrail.TrailRegions
                    .Where(tr => !regionIdsInNewList.Contains(tr.RegionId))
                    .ToList();
                _context.TrailRegions.RemoveRange(regionsToRemove);

                foreach (var newTr in trail.TrailRegions)
                {
                    if (!existingTrail.TrailRegions.Any(et => et.RegionId == newTr.RegionId))
                    {
                        existingTrail.TrailRegions.Add(new TrailRegion { TrailId = existingTrail.Id, RegionId = newTr.RegionId });
                    }
                }
            }
            else
            {
                existingTrail.TrailRegions.Clear();
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
                if (trailRegions.Count != 0)
                {
                    _context.TrailRegions.RemoveRange(trailRegions);
                }
                _context.Trails.Remove(trail);
            }
        }

        public async Task AddTrailsAsync(IEnumerable<Trail> trails)
        {
            await _context.Trails.AddRangeAsync(trails);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> TrailExistsByOsmIdAsync(long osmId)
        {
            return await _context.Trails.AnyAsync(t => t.OsmId == osmId);
        }

        public async Task<Trail?> GetTrailByOsmIdAsync(long osmId)
        {
            var trail = await _context.Trails
               .Include(t => t.TrailRegions)
                   .ThenInclude(tr => tr.Region)
               .Include(t => t.UserRatings)
               .FirstOrDefaultAsync(t => t.OsmId == osmId);

            if (trail?.TrailRegions != null)
            {
                trail.RegionIds = [.. trail.TrailRegions.Select(tr => tr.RegionId)];
                trail.RegionNames = [.. trail.TrailRegions
                                        .Where(tr => tr.Region?.Name != null)
                                        .Select(tr => tr.Region!.Name)];
            }
            return trail;
        }

        public async Task<int> GetTrailCountAsync()
        {
            return await _context.Trails.CountAsync();
        }

        public async Task<List<string>> GetDistinctTrailTypesAsync()
        {
            return await _context.Trails
                .Select(t => t.TrailType)
                .Where(tt => !string.IsNullOrWhiteSpace(tt))
                .Distinct()
                .ToListAsync();
        }
    }
}