using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class TrailMapper : IMapper<Trail, TrailDto>
    {
        public TrailDto ToDto(Trail entity)
        {
            return new TrailDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Distance = entity.Distance,
                Duration = entity.Duration,
                Difficulty = entity.Difficulty,
                TrailType = entity.TrailType,
                StartLocation = entity.StartLocation,
                EndLocation = entity.EndLocation,
                Category = entity.Category,
                Network = entity.Network,
                GeoJsonData = entity.GeoJsonData,
                RegionNames = entity.RegionNames?.Count > 0
                    ? entity.RegionNames
                    : entity.Regions?.Select(r => r.Name).ToList() ?? new List<string>(),
                Tags = entity.Tags
            };
        }

        public Trail ToEntity(TrailDto dto)
        {
            return new Trail
            {
                Id = dto.Id,
                Name = dto.Name,
                Description = dto.Description,
                Distance = dto.Distance,
                Duration = dto.Duration,
                Difficulty = dto.Difficulty,
                TrailType = dto.TrailType,
                StartLocation = dto.StartLocation,
                EndLocation = dto.EndLocation,
                Category = dto.Category,
                Network = dto.Network,
                GeoJsonData = dto.GeoJsonData,
                Tags = dto.Tags
            };
        }
    }
}