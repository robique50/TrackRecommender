using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Data;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;

namespace TrackRecommender.Server.Repositories.Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<User?> GetUserByIdAsync(int id, bool includePreferences = false)
        {
            IQueryable<User> query = _context.Users;

            if (includePreferences)
            {
                query = query.Include(u => u.Preferences);
            }

            return await query.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User?> GetUserByUsernameAsync(string username, bool includePreferences = false)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            IQueryable<User> query = _context.Users;

            if (includePreferences)
            {
                query = query.Include(u => u.Preferences);
            }

            return await query.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task CreateUserAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            await _context.Users.AddAsync(user);
        }

        public Task UpdateUserAsync(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task<UserPreferences?> GetUserPreferencesAsync(int userId)
        {
            return await _context.UserPreferences.FirstOrDefaultAsync(up => up.UserId == userId);
        }

        public async Task CreateUserPreferencesAsync(UserPreferences preferences)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            if (await _context.UserPreferences.AnyAsync(up => up.UserId == preferences.UserId))
            {
                throw new InvalidOperationException($"User with ID {preferences.UserId} already has preferences. Use UpdateUserPreferencesAsync instead.");
            }

            await _context.UserPreferences.AddAsync(preferences);
        }

        public Task UpdateUserPreferencesAsync(UserPreferences preferences)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));

            _context.UserPreferences.Update(preferences);
            return Task.CompletedTask;
        }
        public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(r => r.Token == refreshToken
                       && r.RevokedAt == null
                       && r.ExpiryDate > DateTime.UtcNow)
                .ToListAsync();

            if (!activeTokens.Any())
                return null;

            return await _context.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.Id == activeTokens.First().UserId);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}