using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class UserPreferencesMapper : IMapper<UserPreferences, UserPreferencesDto>
    {
        private readonly IRegionRepository _regionRepository;

        public UserPreferencesMapper(IRegionRepository regionRepository)
        {
            _regionRepository = regionRepository;
        }

        public async Task<UserPreferencesDto> ToDtoAsync(UserPreferences entity)
        {
            var dto = new UserPreferencesDto
            {
                PreferredTrailTypes = entity.PreferredTrailTypes,
                PreferredDifficulty = entity.PreferredDifficulty,
                PreferredTags = entity.PreferredTags,
                MaxDistance = entity.MaxDistance,
                MaxDuration = entity.MaxDuration,
                PreferredCategories = entity.PreferredCategories,
                MinimumRating = entity.MinimumRating
            };

            if (_regionRepository != null && entity.PreferredRegionIds?.Any() == true)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionIdToName = allRegions.ToDictionary(r => r.Id, r => r.Name);

                dto.PreferredRegionNames = entity.PreferredRegionIds
                    .Where(id => regionIdToName.ContainsKey(id))
                    .Select(id => regionIdToName[id])
                    .ToList();
            }
            else
            {
                dto.PreferredRegionNames = new List<string>();
            }

            return dto;
        }

        public UserPreferencesDto ToDto(UserPreferences entity)
        {
            var dto = new UserPreferencesDto
            {
                PreferredTrailTypes = entity.PreferredTrailTypes,
                PreferredDifficulty = entity.PreferredDifficulty,
                PreferredTags = entity.PreferredTags,
                MaxDistance = entity.MaxDistance,
                MaxDuration = entity.MaxDuration,
                PreferredCategories = entity.PreferredCategories,
                MinimumRating = entity.MinimumRating,
                PreferredRegionNames = new List<string>() 
            };

            return dto;
        }

        public async Task<UserPreferences> ToEntityAsync(UserPreferencesDto dto, int userId)
        {
            var entity = new UserPreferences
            {
                UserId = userId,
                PreferredTrailTypes = dto.PreferredTrailTypes ?? new List<string>(),
                PreferredDifficulty = dto.PreferredDifficulty ?? "Easy",
                PreferredTags = dto.PreferredTags ?? new List<string>(),
                MaxDistance = dto.MaxDistance ?? 20,
                MaxDuration = dto.MaxDuration ?? 8,
                PreferredCategories = dto.PreferredCategories ?? new List<string>(),
                MinimumRating = dto.MinimumRating ?? 0,
                PreferredRegionIds = new List<int>()
            };

            if (_regionRepository != null && dto.PreferredRegionNames?.Any() == true)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionNameToId = allRegions.ToDictionary(r => r.Name.ToLower(), r => r.Id);

                entity.PreferredRegionIds = dto.PreferredRegionNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           regionNameToId.ContainsKey(name.ToLower()))
                    .Select(name => regionNameToId[name.ToLower()])
                    .ToList();
            }

            return entity;
        }

        public UserPreferences ToEntity(UserPreferencesDto dto)
        {
            return ToEntity(dto, 0);
        }

        public UserPreferences ToEntity(UserPreferencesDto dto, int userId)
        {
            return new UserPreferences
            {
                UserId = userId,
                PreferredTrailTypes = dto.PreferredTrailTypes ?? new List<string>(),
                PreferredDifficulty = dto.PreferredDifficulty ?? "Easy",
                PreferredTags = dto.PreferredTags ?? new List<string>(),
                MaxDistance = dto.MaxDistance ?? 20,
                MaxDuration = dto.MaxDuration ?? 8,
                PreferredCategories = dto.PreferredCategories ?? new List<string>(),
                MinimumRating = dto.MinimumRating ?? 0,
                PreferredRegionIds = new List<int>() 
            };
        }

        public async Task UpdateEntityAsync(UserPreferences entity, UserPreferencesDto dto)
        {
            entity.PreferredTrailTypes = dto.PreferredTrailTypes ?? entity.PreferredTrailTypes;
            entity.PreferredDifficulty = dto.PreferredDifficulty ?? entity.PreferredDifficulty;
            entity.PreferredTags = dto.PreferredTags ?? entity.PreferredTags;
            entity.MaxDistance = dto.MaxDistance ?? entity.MaxDistance;
            entity.MaxDuration = dto.MaxDuration ?? entity.MaxDuration;
            entity.PreferredCategories = dto.PreferredCategories ?? entity.PreferredCategories;
            entity.MinimumRating = dto.MinimumRating ?? entity.MinimumRating;

            if (_regionRepository != null && dto.PreferredRegionNames?.Any() == true)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionNameToId = allRegions.ToDictionary(r => r.Name.ToLower(), r => r.Id);

                entity.PreferredRegionIds = dto.PreferredRegionNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           regionNameToId.ContainsKey(name.ToLower()))
                    .Select(name => regionNameToId[name.ToLower()])
                    .ToList();
            }
        }

        public void UpdateEntity(UserPreferences entity, UserPreferencesDto dto)
        {
            entity.PreferredTrailTypes = dto.PreferredTrailTypes ?? entity.PreferredTrailTypes;
            entity.PreferredDifficulty = dto.PreferredDifficulty ?? entity.PreferredDifficulty;
            entity.PreferredTags = dto.PreferredTags ?? entity.PreferredTags;
            entity.MaxDistance = dto.MaxDistance ?? entity.MaxDistance;
            entity.MaxDuration = dto.MaxDuration ?? entity.MaxDuration;
            entity.PreferredCategories = dto.PreferredCategories ?? entity.PreferredCategories;
            entity.MinimumRating = dto.MinimumRating ?? entity.MinimumRating;
        }
    }
}