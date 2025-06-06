using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using NetTopologySuite.IO;
using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Services
{
    public class RegionService(
        IRegionRepository regionRepository, 
        ILogger<RegionService> logger,
        IMapper<Trail, TrailDto> trailMapper
        )
    {
        private readonly IRegionRepository _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        private readonly ILogger<RegionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly GeoJsonWriter _geoJsonWriter = new();
        private readonly IMapper<Trail, TrailDto> _trailMapper = trailMapper ?? throw new ArgumentNullException(nameof(trailMapper));

        public async Task<List<RegionDto>> GetAllRegionsAsync()
        {
            try
            {
                var regions = await _regionRepository.GetAllRegionsAsync();
                var regionDtos = new List<RegionDto>();

                foreach (var region in regions)
                {
                    var trailCount = await _regionRepository.GetTrailCountByRegionIdAsync(region.Id);
                    
                    string boundaryGeoJson = string.Empty;
                    if (region.Boundary != null && !region.Boundary.IsEmpty)
                    {
                        try
                        {
                            boundaryGeoJson = _geoJsonWriter.Write(region.Boundary);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to serialize boundary for region {RegionId} ({RegionName})", region.Id, region.Name);
                        }
                    }

                    regionDtos.Add(new RegionDto
                    {
                        Id = region.Id,
                        Name = region.Name,
                        TrailCount = trailCount,
                        BoundaryGeoJson = boundaryGeoJson
                    });
                }

                return regionDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all regions");
                throw;
            }
        }

        public async Task<RegionDto?> GetRegionByIdAsync(int id)
        {
            try
            {
                var region = await _regionRepository.GetRegionByIdAsync(id);
                if (region == null)
                    return null;

                var trailCount = await _regionRepository.GetTrailCountByRegionIdAsync(region.Id);

                string boundaryGeoJson = string.Empty;
                if (region.Boundary != null && !region.Boundary.IsEmpty)
                {
                    try
                    {
                        boundaryGeoJson = _geoJsonWriter.Write(region.Boundary);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to serialize boundary for region {RegionId} ({RegionName})", region.Id, region.Name);
                    }
                }

                return new RegionDto
                {
                    Id = region.Id,
                    Name = region.Name,
                    TrailCount = trailCount,
                    BoundaryGeoJson = boundaryGeoJson
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving region with ID {RegionId}", id);
                throw;
            }
        }

        public async Task<List<TrailDto>> GetTrailsByRegionIdAsync(int regionId)
        {
            try
            {
                var trails = await _regionRepository.GetTrailsByRegionIdAsync(regionId);

                return [.. trails.Select(t => _trailMapper.ToDto(t))];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trails for region {RegionId}", regionId);
                throw;
            }
        }
    }
}