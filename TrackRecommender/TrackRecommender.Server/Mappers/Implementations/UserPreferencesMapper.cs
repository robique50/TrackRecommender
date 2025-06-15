using System.Text.Json;
using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using TrackRecommender.Server.Services;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class UserPreferencesMapper(IRegionRepository regionRepository, TrailMarkingService markingService) : IMapper<UserPreferences, UserPreferencesDto>
    {
        private readonly IRegionRepository _regionRepository = regionRepository;
        private readonly TrailMarkingService _markingService = markingService;

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
                MinimumRating = entity.MinimumRating,
                PreferredMarkings = []
            };

            if (!string.IsNullOrEmpty(entity.MarkingPreferences))
            {
                try
                {
                    dto.PreferredMarkings = JsonSerializer.Deserialize<List<TrailMarkingDto>>(
                        entity.MarkingPreferences) ?? [];
                }
                catch (Exception)
                {
                    dto.PreferredMarkings = [];
                }
            }

            if (_regionRepository != null && entity.PreferredRegionIds?.Count > 0)
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
                PreferredRegionNames = [],
                PreferredMarkings = []
            };

            if (!string.IsNullOrEmpty(entity.MarkingPreferences))
            {
                try
                {
                    dto.PreferredMarkings = JsonSerializer.Deserialize<List<TrailMarkingDto>>(
                        entity.MarkingPreferences) ?? [];
                }
                catch (Exception)
                {
                    dto.PreferredMarkings = [];
                }
            }

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

            if (dto.PreferredMarkings?.Count > 0)
            {
                entity.MarkingPreferences = JsonSerializer.Serialize(dto.PreferredMarkings);

                foreach (var marking in dto.PreferredMarkings)
                {
                    if (!string.IsNullOrEmpty(marking.Symbol) &&
                        !entity.PreferredTags.Contains($"osmc:symbol={marking.Symbol}"))
                    {
                        entity.PreferredTags.Add($"osmc:symbol={marking.Symbol}");
                    }
                }
            }

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

            if (dto.PreferredMarkings?.Count > 0)
            {
                entity.MarkingPreferences = JsonSerializer.Serialize(dto.PreferredMarkings);

                foreach (var marking in dto.PreferredMarkings)
                {
                    if (!string.IsNullOrEmpty(marking.Symbol) &&
                        !entity.PreferredTags.Contains($"osmc:symbol={marking.Symbol}"))
                    {
                        entity.PreferredTags.Add($"osmc:symbol={marking.Symbol}");
                    }
                }
            }

            return entity;
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

            if (dto.PreferredMarkings != null)
            {
                if (dto.PreferredMarkings.Count > 0)
                {
                    entity.MarkingPreferences = JsonSerializer.Serialize(dto.PreferredMarkings);

                    entity.PreferredTags.RemoveAll(t => t.StartsWith("osmc:symbol="));

                    foreach (var marking in dto.PreferredMarkings)
                    {
                        if (!string.IsNullOrEmpty(marking.Symbol) &&
                            !entity.PreferredTags.Contains($"osmc:symbol={marking.Symbol}"))
                        {
                            entity.PreferredTags.Add($"osmc:symbol={marking.Symbol}");
                        }
                    }
                }
                else
                {
                    entity.MarkingPreferences = null;
                    entity.PreferredTags.RemoveAll(t => t.StartsWith("osmc:symbol="));
                }
            }

            if (_regionRepository != null && dto.PreferredRegionNames?.Count > 0)
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

            if (dto.PreferredMarkings != null)
            {
                if (dto.PreferredMarkings.Count > 0)
                {
                    entity.MarkingPreferences = JsonSerializer.Serialize(dto.PreferredMarkings);

                    entity.PreferredTags.RemoveAll(t => t.StartsWith("osmc:symbol="));

                    foreach (var marking in dto.PreferredMarkings)
                    {
                        if (!string.IsNullOrEmpty(marking.Symbol) &&
                            !entity.PreferredTags.Contains($"osmc:symbol={marking.Symbol}"))
                        {
                            entity.PreferredTags.Add($"osmc:symbol={marking.Symbol}");
                        }
                    }
                }
                else
                {
                    entity.MarkingPreferences = null;
                    entity.PreferredTags.RemoveAll(t => t.StartsWith("osmc:symbol="));
                }
            }
        }
    }
}