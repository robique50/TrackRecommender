using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class RegionRepository(AppDbContext context) : IRegionRepository
    {
        private readonly AppDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<List<Region>> GetAllRegionsAsync(bool includeTrails = false)
        {
            IQueryable<Region> query = _context.Regions;

            if (includeTrails)
            {
                query = query.Include(r => r.Trails);
            }

            return await query.ToListAsync();
        }

        public async Task<Region?> GetRegionByIdAsync(int id, bool includeTrails = false)
        {
            if (includeTrails)
            {
                return await _context.Regions
                    .Include(r => r.Trails)
                    .FirstOrDefaultAsync(r => r.Id == id);
            }

            return await _context.Regions.FindAsync(id);
        }

        public async Task<Region?> GetRegionByNameAsync(string name, bool includeTrails = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            IQueryable<Region> query = _context.Regions;

            if (includeTrails)
            {
                query = query.Include(r => r.Trails);
            }

            return await query
                .FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower());
        }

        public async Task AddRegionAsync(Region region)
        {
            ArgumentNullException.ThrowIfNull(region);

            bool nameExists = await _context.Regions
                .AnyAsync(r => r.Name.ToLower() == region.Name.ToLower() && r.Id != region.Id);

            if (nameExists)
            {
                throw new InvalidOperationException($"A region with the name '{region.Name}' already exists.");
            }

            await _context.Regions.AddAsync(region);
        }

        public async Task<List<Region>> GetRegionsByTrailIdAsync(int trailId)
        {
            var regions = await _context.TrailRegions
                .Where(tr => tr.TrailId == trailId && tr.Region != null)
                .Select(tr => tr.Region!)
                .ToListAsync();

            return regions;
        }

        public async Task<List<string>> GetRegionNamesByTrailIdAsync(int trailId)
        {
            var regionNames = await _context.TrailRegions
                .Where(tr => tr.TrailId == trailId && tr.Region != null && tr.Region.Name != null)
                .Select(tr => tr.Region!.Name)
                .ToListAsync();

            return regionNames;
        }

        public async Task<int> GetTrailCountByRegionIdAsync(int regionId)
        {
            return await _context.TrailRegions
                .CountAsync(tr => tr.RegionId == regionId);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public Task UpdateRegion(Region region)
        {
            ArgumentNullException.ThrowIfNull(region);
            _context.Regions.Update(region);
            return Task.CompletedTask;
        }

        public async Task DeleteRegionAsync(int regionId)
        {
            var region = await _context.Regions.FindAsync(regionId);
            if (region != null)
            {
                var trailRegions = await _context.TrailRegions
                    .Where(tr => tr.RegionId == regionId)
                    .ToListAsync();

                _context.TrailRegions.RemoveRange(trailRegions);
                _context.Regions.Remove(region);
            }
        }

        public async Task<List<Trail>> GetTrailsByRegionIdAsync(int regionId)
        {
            try
            {
                var trailIds = await _context.TrailRegions
                    .Where(tr => tr.RegionId == regionId)
                    .Select(tr => tr.TrailId)
                    .ToListAsync();

                var trails = await _context.Trails
                    .Where(t => trailIds.Contains(t.Id))
                    .Include(t => t.UserRatings)
                    .ToListAsync();

                foreach (var trail in trails)
                {
                    var trailRegions = await _context.TrailRegions
                        .Where(tr => tr.TrailId == trail.Id)
                        .Include(tr => tr.Region)
                        .ToListAsync();

                    trail.RegionIds = [.. trailRegions.Select(tr => tr.RegionId)];
                    trail.RegionNames = [.. trailRegions
                        .Where(tr => tr.Region?.Name != null)
                        .Select(tr => tr.Region!.Name)];
                }

                return trails;
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Failed to retrieve trails for region with ID {regionId}. ");            }
        }

        public async Task<int> GetTrailCountForRegionAsync(int regionId)
        {
            return await _context.TrailRegions
                .Where(tr => tr.RegionId == regionId)
                .CountAsync();
        }
    }
}