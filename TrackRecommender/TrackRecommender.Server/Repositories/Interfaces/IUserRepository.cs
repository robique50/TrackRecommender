using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetUserByIdAsync(int id, bool includePreferences = false);
        Task<User?> GetUserByUsernameAsync(string username, bool includePreferences = false);
        Task<User?> GetUserByEmailAsync(string email);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        Task CreateUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task<UserPreferences?> GetUserPreferencesAsync(int userId);
        Task CreateUserPreferencesAsync(UserPreferences preferences);
        Task UpdateUserPreferencesAsync(UserPreferences preferences);
        Task DeleteUserPreferencesAsync(UserPreferences preferences);
        Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
        Task<int> SaveChangesAsync();
    }
}