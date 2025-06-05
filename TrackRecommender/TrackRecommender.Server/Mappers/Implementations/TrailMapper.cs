using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class TrailMapper(ILogger<TrailMapper> logger) : IMapper<Trail, TrailDto>
    {
        private readonly GeoJsonWriter _geoJsonWriter = new();
        private readonly GeoJsonReader _geoJsonReader = new();
        private readonly ILogger<TrailMapper> _logger = logger;

        public TrailDto ToDto(Trail entity)
        {
            string geoJsonData = string.Empty;
            int pointCount = 0;
            string geometryType = "Unknown";

            try
            {
                if (entity.Coordinates != null && !entity.Coordinates.IsEmpty && entity.Coordinates.IsValid)
                {
                    geoJsonData = _geoJsonWriter.Write(entity.Coordinates);

                    pointCount = entity.Coordinates.NumPoints;
                    geometryType = entity.Coordinates.GeometryType;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to serialize geometry for trail {TrailId}: {Message}", entity.Id, ex.Message);
                geoJsonData = string.Empty;
            }

            return new TrailDto
            {
                Id = entity.Id,
                OsmId = entity.OsmId,
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
                GeoJsonData = geoJsonData,
                RegionNames = entity.TrailRegions?
                    .Where(tr => tr.Region?.Name != null)
                    .Select(tr => tr.Region!.Name)
                    .Distinct()
                    .ToList() ?? [],
                RegionIds = entity.TrailRegions?
                    .Select(tr => tr.RegionId)
                    .Distinct()
                    .ToList() ?? [],
                Tags = entity.Tags ?? [],
                AverageRating = (entity.UserRatings != null && entity.UserRatings.Count > 0) ?
                    entity.UserRatings.Average(r => r.Rating) : 0,
                ReviewsCount = entity.UserRatings?.Count ?? 0,
                LastUpdated = entity.LastUpdated,
                Coordinates = entity.Coordinates
            };
        }

        public Trail ToEntity(TrailDto dto)
        {
            Geometry? parsedGeometry = null;

            try
            {
                if (!string.IsNullOrEmpty(dto.GeoJsonData))
                {
                    parsedGeometry = _geoJsonReader.Read<Geometry>(dto.GeoJsonData);

                    if (parsedGeometry != null && parsedGeometry.SRID == 0)
                    {
                        parsedGeometry.SRID = 4326;
                    }
                }
                else if (dto.Coordinates != null)
                {
                    parsedGeometry = dto.Coordinates;
                    if (parsedGeometry.SRID == 0)
                    {
                        parsedGeometry.SRID = 4326;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse geometry for trail DTO: {Message}", ex.Message);
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                parsedGeometry = geometryFactory.CreatePoint(new Coordinate(0, 0));
            }

            if (parsedGeometry == null)
            {
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                parsedGeometry = geometryFactory.CreatePoint(new Coordinate(0, 0));
            }

            var trailEntity = new Trail
            {
                Id = dto.Id,
                OsmId = dto.OsmId,
                Name = dto.Name ?? string.Empty,
                Description = dto.Description ?? string.Empty,
                Distance = dto.Distance,
                Duration = dto.Duration,
                Difficulty = dto.Difficulty ?? string.Empty,
                TrailType = dto.TrailType ?? string.Empty,
                StartLocation = dto.StartLocation ?? string.Empty,
                EndLocation = dto.EndLocation ?? string.Empty,
                Category = dto.Category ?? "Local",
                Network = dto.Network,
                Coordinates = parsedGeometry,
                Tags = dto.Tags ?? [],
                LastUpdated = DateTime.UtcNow,
            };

            if (dto.RegionIds != null && dto.RegionIds.Count > 0)
            {
                trailEntity.TrailRegions = [.. dto.RegionIds.Select(regionId => new TrailRegion { RegionId = regionId })];
            }

            return trailEntity;
        }
    }
}