using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class RegionRepository : IRegionRepository
    {
        private readonly AppDbContext _context;

        public RegionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Region>> GetAllRegionsAsync()
        {
            return await _context.Regions.ToListAsync();
        }

        public async Task<Region?> GetRegionByIdAsync(int id)
        {
            return await _context.Regions.FindAsync(id);
        }

        public async Task<Region?> GetRegionByNameAsync(string name)
        {
            return await _context.Regions
                .FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower());
        }

        public async Task AddRegionAsync(Region region)
        {
            await _context.Regions.AddAsync(region);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
