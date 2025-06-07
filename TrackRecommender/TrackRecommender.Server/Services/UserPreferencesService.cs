using TrackRecommender.Server.Mappers.Implementations;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Services
{
    public class UserPreferencesService(
        IUserRepository userRepository,
        IRegionRepository regionRepository,
        ITrailRepository trailRepository,
        UserPreferencesMapper preferencesMapper)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRegionRepository _regionRepository = regionRepository;
        private readonly ITrailRepository _trailRepository = trailRepository;
        private readonly UserPreferencesMapper _preferencesMapper = preferencesMapper;
        public async Task<UserPreferencesDto?> GetUserPreferencesAsync(int userId)
        {
            var preferences = await _userRepository.GetUserPreferencesAsync(userId);
            return preferences != null
                ? await _preferencesMapper.ToDtoAsync(preferences)
                : new UserPreferencesDto();
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

        public async Task ResetUserPreferencesAsync(int userId)
        {
            var existingPreferences = await _userRepository.GetUserPreferencesAsync(userId);

            if (existingPreferences != null)
            {
                await _userRepository.DeleteUserPreferencesAsync(existingPreferences);
                await _userRepository.SaveChangesAsync();
            }
        }

        public async Task<PreferenceOptionsDto> GetPreferenceOptionsAsync()
        {
            var trailTypes = await _trailRepository.GetDistinctTrailTypesAsync();

            var regions = await _regionRepository.GetAllRegionsAsync();

            var regionOptions = new List<RegionOptionDto>();
            foreach (var region in regions)
            {
                int trailCount = await _regionRepository.GetTrailCountForRegionAsync(region.Id);

                regionOptions.Add(new RegionOptionDto
                {
                    Id = region.Id,
                    Name = region.Name,
                    TrailCount = trailCount
                });
            }

            return new PreferenceOptionsDto
            {
                TrailTypes = trailTypes,
                Regions = [.. regionOptions.OrderBy(r => r.Name)]
            };
        }
    }
}