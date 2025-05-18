using TrackRecommender.Server.Mappers.Interfaces;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Mappers.Implementations
{
    public class UserMapper : IMapper<User, UserProfileDto>
    {
        public UserProfileDto ToDto(User entity)
        {
            return new UserProfileDto
            {
                Id = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                CreatedAt = entity.CreatedAt,
                LastLoginAt = entity.LastLoginAt,
                Role = entity.Role,
                HasPreferences = entity.Preferences != null
            };
        }

        public User ToEntity(UserProfileDto dto)
        {
            return new User
            {
                Id = dto.Id,
                Username = dto.Username,
                Email = dto.Email,
                CreatedAt = dto.CreatedAt,
                LastLoginAt = dto.LastLoginAt,
                Role = dto.Role
            };
        }
    }
}
