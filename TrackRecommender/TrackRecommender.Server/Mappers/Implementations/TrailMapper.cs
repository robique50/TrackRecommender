using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using NetTopologySuite.Geometries;

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
                Coordinates = entity.Coordinates,
                RegionNames = entity.TrailRegions? 
                    .Where(tr => tr.Region != null) 
                    .Select(tr => tr.Region.Name)
                    .Where(name => name != null)
                    .ToList() ?? new List<string>(),
                Tags = entity.Tags ?? new List<string>(),
                AverageRating = entity.UserRatings?.Any() == true ? entity.UserRatings.Average(r => r.Rating) : 0,
                ReviewsCount = entity.UserRatings?.Count ?? 0,
            };
        }

        public Trail ToEntity(TrailDto dto)
        {
            var trailEntity = new Trail 
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
                Coordinates = dto.Coordinates ?? new LineString(new Coordinate[] { }),
                Tags = dto.Tags ?? new List<string>(),
            };
            return trailEntity;
        }
    }
}