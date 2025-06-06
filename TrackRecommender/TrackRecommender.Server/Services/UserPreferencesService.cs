using TrackRecommender.Server.Mappers.Implementations;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;

namespace TrackRecommender.Server.Services
{
    public class UserPreferencesService(
        IUserRepository userRepository,
        IRegionRepository regionRepository,
        AppDbContext context,
        UserPreferencesMapper preferencesMapper,
        ILogger<UserPreferencesService> logger)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRegionRepository _regionRepository = regionRepository;
        private readonly AppDbContext _context = context;
        private readonly UserPreferencesMapper _preferencesMapper = preferencesMapper;
        private readonly ILogger<UserPreferencesService> _logger = logger;

        public async Task<UserPreferencesDto?> GetUserPreferencesAsync(int userId)
        {
            var preferences = await _userRepository.GetUserPreferencesAsync(userId);
            if (preferences == null)
                return null;

            return await _preferencesMapper.ToDtoAsync(preferences);
        }

        public async Task SaveUserPreferencesAsync(int userId, UserPreferencesDto preferencesDto)
        {
            var existingPreferences = await _userRepository.GetUserPreferencesAsync(userId);

            if (existingPreferences != null)
            {
                await _preferencesMapper.UpdateEntityAsync(existingPreferences, preferencesDto);
                await _userRepository.UpdateUserPreferencesAsync(existingPreferences);
            }
            else
            {
                var newPreferences = await _preferencesMapper.ToEntityAsync(preferencesDto, userId);
                await _userRepository.CreateUserPreferencesAsync(newPreferences);
            }

            await _userRepository.SaveChangesAsync();
        }

        public async Task<PreferenceOptionsDto> GetPreferenceOptionsAsync()
        {
            try
            {
                var trails = await _context.Trails.AsNoTracking().ToListAsync();

                var trailTypes = trails
                    .Select(t => t.TrailType)
                    .Where(tt => !string.IsNullOrEmpty(tt))
                    .Distinct()
                    .OrderBy(tt => tt)
                    .ToList();

                var difficulties = trails
                    .Select(t => t.Difficulty)
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .OrderBy(d => GetDifficultyOrder(d))
                    .ToList();

                var categories = trails
                    .Select(t => t.Category)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .OrderBy(c => GetCategoryOrder(c))
                    .ToList();

                var allTags = new HashSet<string>();
                foreach (var trail in trails)
                {
                    if (trail.Tags != null)
                    {
                        foreach (var tag in trail.Tags)
                        {
                            if (tag.Contains('='))
                            {
                                var parts = tag.Split('=', 2);
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();

                                switch (key)
                                {
                                    case "route":
                                    case "network":
                                    case "symbol":
                                    case "operator":
                                    case "osmc:symbol":
                                        allTags.Add(tag);
                                        break;
                                    case "mountain_feature":
                                    case "tourist_feature":
                                    case "seasonal_access":
                                    case "technical_difficulty":
                                        allTags.Add(tag);
                                        break;
                                }
                            }
                        }
                    }
                }

                var regions = await _regionRepository.GetAllRegionsAsync();
                var regionOptions = regions
                    .OrderBy(r => r.Name)
                    .Select(r => new RegionOptionDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        TrailCount = 0
                    })
                    .ToList();

                foreach (var region in regionOptions)
                {
                    region.TrailCount = await _context.TrailRegions
                        .CountAsync(tr => tr.RegionId == region.Id);
                }

                return new PreferenceOptionsDto
                {
                    TrailTypes = trailTypes,
                    Difficulties = difficulties,
                    Categories = categories,
                    AvailableTags = [.. allTags.OrderBy(t => t)],
                    Regions = regionOptions,
                    MinDistance = 0.5,
                    MaxDistance = trails.Count != 0 ? trails.Max(t => t.Distance) : 500,
                    MinDuration = 0.25,
                    MaxDuration = trails.Count != 0 ? trails.Max(t => t.Duration) : 48
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving preference options");
                throw;
            }
        }

        private static int GetDifficultyOrder(string difficulty)
        {
            return difficulty switch
            {
                "Easy" => 1,
                "Moderate" => 2,
                "Difficult" => 3,
                "Very Difficult" => 4,
                "Expert" => 5,
                _ => 6
            };
        }

        private static int GetCategoryOrder(string category)
        {
            return category switch
            {
                "International" => 1,
                "National" => 2,
                "Regional" => 3,
                "Local" => 4,
                _ => 5
            };
        }
    }
}