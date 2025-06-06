using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class UserPreferencesMapper(IRegionRepository regionRepository) : IMapper<UserPreferences, UserPreferencesDto>
    {
        private readonly IRegionRepository _regionRepository = regionRepository;

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

            if (_regionRepository != null && entity.PreferredRegionIds?.Count>0)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionIdToName = allRegions.ToDictionary(r => r.Id, r => r.Name);

                dto.PreferredRegionNames = [.. entity.PreferredRegionIds
                    .Where(id => regionIdToName.ContainsKey(id))
                    .Select(id => regionIdToName[id])];
            }
            else
            {
                dto.PreferredRegionNames = [];
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
                PreferredRegionNames = [] 
            };

            return dto;
        }

        public async Task<UserPreferences> ToEntityAsync(UserPreferencesDto dto, int userId)
        {
            var entity = new UserPreferences
            {
                UserId = userId,
                PreferredTrailTypes = dto.PreferredTrailTypes ?? [],
                PreferredDifficulty = dto.PreferredDifficulty ?? "Easy",
                PreferredTags = dto.PreferredTags ?? [],
                MaxDistance = dto.MaxDistance ?? 20,
                MaxDuration = dto.MaxDuration ?? 8,
                PreferredCategories = dto.PreferredCategories ?? [],
                MinimumRating = dto.MinimumRating ?? 0,
                PreferredRegionIds = []
            };

            if (_regionRepository != null && dto.PreferredRegionNames?.Count > 0)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionNameToId = allRegions.ToDictionary(r => r.Name.ToLower(), r => r.Id);

                entity.PreferredRegionIds = [.. dto.PreferredRegionNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           regionNameToId.ContainsKey(name.ToLower()))
                    .Select(name => regionNameToId[name.ToLower()])];
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
                PreferredTrailTypes = dto.PreferredTrailTypes ?? [],
                PreferredDifficulty = dto.PreferredDifficulty ?? "Easy",
                PreferredTags = dto.PreferredTags ?? [],
                MaxDistance = dto.MaxDistance ?? 20,
                MaxDuration = dto.MaxDuration ?? 8,
                PreferredCategories = dto.PreferredCategories ?? [],
                MinimumRating = dto.MinimumRating ?? 0,
                PreferredRegionIds = [] 
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

            if (_regionRepository != null && dto.PreferredRegionNames?.Count>0)
            {
                var allRegions = await _regionRepository.GetAllRegionsAsync();
                var regionNameToId = allRegions.ToDictionary(r => r.Name.ToLower(), r => r.Id);

                entity.PreferredRegionIds = [.. dto.PreferredRegionNames
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           regionNameToId.ContainsKey(name.ToLower()))
                    .Select(name => regionNameToId[name.ToLower()])];
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