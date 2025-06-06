using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class ReviewMapper : IMapper<UserTrailRating, TrailReviewDto>
    {
        public TrailReviewDto ToDto(UserTrailRating entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            return new TrailReviewDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Username = entity.User?.Username ?? "Unknown",
                TrailId = entity.TrailId,
                TrailName = entity.Trail?.Name ?? "Unknown Trail",
                Rating = entity.Rating,
                Comment = entity.Comment,
                HasCompleted = entity.HasCompleted,
                RatedAt = entity.RatedAt,
                CompletedAt = entity.CompletedAt,
                ActualDuration = entity.ActualDuration,
                PerceivedDifficulty = entity.PerceivedDifficulty
            };
        }

        public UserTrailRating ToEntity(TrailReviewDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new UserTrailRating
            {
                Id = dto.Id,
                UserId = dto.UserId,
                TrailId = dto.TrailId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                HasCompleted = dto.HasCompleted,
                RatedAt = dto.RatedAt,
                CompletedAt = dto.CompletedAt,
                ActualDuration = dto.ActualDuration,
                PerceivedDifficulty = dto.PerceivedDifficulty
            };
        }

        public UserTrailRating MapCreateDtoToEntity(CreateReviewDto createDto, int userId, int trailId)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            return new UserTrailRating
            {
                UserId = userId,
                TrailId = trailId,
                Rating = createDto.Rating,
                Comment = createDto.Comment?.Trim(),
                HasCompleted = createDto.HasCompleted,
                CompletedAt = createDto.CompletedAt,
                ActualDuration = createDto.ActualDuration,
                PerceivedDifficulty = createDto.PerceivedDifficulty?.Trim(),
                RatedAt = DateTime.UtcNow
            };
        }
    }
}