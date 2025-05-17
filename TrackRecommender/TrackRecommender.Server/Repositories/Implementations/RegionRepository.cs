using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class RegionRepository : IRegionRepository
    {
        private readonly AppDbContext _context;

        public RegionRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

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
            if (region == null)
                throw new ArgumentNullException(nameof(region));

            bool nameExists = await _context.Regions.AnyAsync(r => r.Name.ToLower() == region.Name.ToLower() && r.Id != region.Id);
            if (nameExists)
            {
                throw new InvalidOperationException($"A region with the name '{region.Name}' already exists.");
            }

            await _context.Regions.AddAsync(region);
        }

        public async Task<List<Region>> GetRegionsByTrailIdAsync(int trailId)
        {
            var regions = await _context.TrailRegions
                .Where(tr => tr.TrailId == trailId)
                .Select(tr => tr.Region)
                .ToListAsync();

            return regions;
        }

        public async Task<List<string>> GetRegionNamesByTrailIdAsync(int trailId)
        {
            var regionNames = await _context.TrailRegions
                .Where(tr => tr.TrailId == trailId)
                .Select(tr => tr.Region.Name)
                .ToListAsync();

            return regionNames;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public Task UpdateRegion(Region region)
        {
            if (region == null)
                throw new ArgumentNullException(nameof(region));
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
    }
}