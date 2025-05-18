using TrackRecommender.Server.Mappers.Implementations;
using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Services.Implementations
{
    public class UserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRegionRepository _regionRepository;
        private readonly IMapper<User, UserProfileDto> _userMapper;
        private readonly UserPreferencesMapper _preferencesMapper; 

        public UserService(
            IUserRepository userRepository,
            IRegionRepository regionRepository,
            IMapper<User, UserProfileDto> userMapper,
            UserPreferencesMapper preferencesMapper) 
        {
            _userRepository = userRepository;
            _regionRepository = regionRepository;
            _userMapper = userMapper;
            _preferencesMapper = preferencesMapper;
        }

        public async Task<UserProfileDto> GetUserProfileAsync(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId, includePreferences: true);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            return _userMapper.ToDto(user);
        }

        public async Task<UserPreferencesDto> GetUserPreferencesAsync(int userId)
        {
            var preferences = await _userRepository.GetUserPreferencesAsync(userId);
            if (preferences == null)
                throw new KeyNotFoundException($"Preferences for user with ID {userId} not found");

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
    }
}