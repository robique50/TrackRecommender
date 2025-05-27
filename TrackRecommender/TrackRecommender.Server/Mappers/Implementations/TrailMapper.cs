using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class TrailMapper : IMapper<Trail, TrailDto>
    {
        private readonly GeoJsonWriter _geoJsonWriter;
        private readonly GeoJsonReader _geoJsonReader;
        
        public TrailMapper()
        {
            _geoJsonWriter = new GeoJsonWriter();
            _geoJsonReader = new GeoJsonReader();
        }

        public TrailDto ToDto(Trail entity)
        {
            string geoJsonData = string.Empty;
            
            try
            {
                if (entity.Coordinates != null && entity.Coordinates.IsValid)
                {
                    geoJsonData = _geoJsonWriter.Write(entity.Coordinates);
                }
            }
            catch (Exception)
            {
                geoJsonData = string.Empty;
            }

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
                GeoJsonData = geoJsonData,
                RegionNames = entity.TrailRegions? 
                    .Where(tr => tr.Region != null) 
                    .Select(tr => tr.Region.Name)
                    .Where(name => name != null)
                    .ToList() ?? [],
                Tags = entity.Tags ?? [],
                AverageRating = entity.UserRatings?.Any() == true ? entity.UserRatings.Average(r => r.Rating) : 0,
                ReviewsCount = entity.UserRatings?.Count ?? 0,
            };
        }

        public Trail ToEntity(TrailDto dto)
        {
            LineString coordinates = new([]);
            
            try
            {
                if (!string.IsNullOrEmpty(dto.GeoJsonData))
                {
                    var geometry = _geoJsonReader.Read<LineString>(dto.GeoJsonData);
                    if (geometry != null && geometry.IsValid)
                    {
                        coordinates = geometry;
                    }
                }
                else if (dto.Coordinates != null && dto.Coordinates.IsValid)
                {
                    coordinates = dto.Coordinates;
                }
            }
            catch (Exception)
            {
                coordinates = new LineString([]);
            }

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
                Coordinates = coordinates,
                Tags = dto.Tags ?? [],
            };
            
            return trailEntity;
        }
    }
}