using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using System.Security.Cryptography;

namespace TrackRecommender.Server.Services.Implementations
{
    public class AuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<TokenResponseDto?> AuthenticateAsync(LoginDto loginDto, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
                return null;

            var user = await _userRepository.GetUserByUsernameAsync(loginDto.Username);
            if (user == null)
                return null;

            if (!VerifyPasswordHash(loginDto.Password, user.PasswordHash))
                return null;

            var romaniaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GTB Standard Time");
            user.LastLoginAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, romaniaTimeZone);

            var jwtToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken(ipAddress);

            user.RefreshTokens.Add(refreshToken);

            RemoveOldRefreshTokens(user);

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return new TokenResponseDto
            {
                AccessToken = jwtToken,
                RefreshToken = refreshToken.Token,
                Expiration = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpirationInMinutes"))
            };
        }

        public async Task<TokenResponseDto?> RefreshTokenAsync(string token, string ipAddress)
        {
            var user = await _userRepository.GetUserByRefreshTokenAsync(token);
            if (user == null) return null;

            var refreshToken = user.RefreshTokens.SingleOrDefault(r =>
                r.Token == token &&
                r.RevokedAt == null &&
                r.ExpiryDate > DateTime.UtcNow);

            if (refreshToken == null) return null;

            var newRefreshToken = GenerateRefreshToken(ipAddress);
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            user.RefreshTokens.Add(newRefreshToken);

            RemoveOldRefreshTokens(user);

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            var jwtToken = GenerateJwtToken(user);

            return new TokenResponseDto
            {
                AccessToken = jwtToken,
                RefreshToken = newRefreshToken.Token,
                Expiration = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:ExpirationInMinutes"))
            };
        }

        public async Task<bool> RevokeTokenAsync(string token, string ipAddress)
        {
            var user = await _userRepository.GetUserByRefreshTokenAsync(token);
            if (user == null) return false;

            var refreshToken = user.RefreshTokens.SingleOrDefault(r =>
                r.Token == token &&
                r.RevokedAt == null &&
                r.ExpiryDate > DateTime.UtcNow);

            if (refreshToken == null) return false;

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return true;
        }

        private void RemoveOldRefreshTokens(User user)
        {
            var activeTokens = user.RefreshTokens
                .Where(x => x.RevokedAt == null && x.ExpiryDate > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToList();

            var recentRevokedTokens = user.RefreshTokens
                .Where(x => x.RevokedAt != null)
                .OrderByDescending(x => x.RevokedAt)
                .Take(5)
                .ToList();

            var tokensToKeep = activeTokens.Union(recentRevokedTokens).ToList();

            user.RefreshTokens.Clear();
            foreach (var token in tokensToKeep)
            {
                user.RefreshTokens.Add(token);
            }
        }

        private RefreshToken GenerateRefreshToken(string ipAddress)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[64];
            rng.GetBytes(randomBytes);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(RegisterDto registerDto)
        {
            if (string.IsNullOrWhiteSpace(registerDto.Username) ||
                string.IsNullOrWhiteSpace(registerDto.Email) ||
                string.IsNullOrWhiteSpace(registerDto.Password))
            {
                return (false, "All fields are required");
            }

            if (await _userRepository.UsernameExistsAsync(registerDto.Username))
            {
                return (false, "Username already exists");
            }

            if (await _userRepository.EmailExistsAsync(registerDto.Email))
            {
                return (false, "Email already exists");
            }

            var passwordHash = CreatePasswordHash(registerDto.Password);

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                Role = "User"
            };

            await _userRepository.CreateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, null);
        }

        private string GenerateJwtToken(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is not configured");
            }

            var minutes = _configuration.GetValue<int>("Jwt:ExpirationInMinutes");
            var expirationMinutes = minutes > 0 ? minutes : 1440;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(jwtKey);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role)
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"]
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                return tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error generating JWT token: {ex.Message}", ex);
            }
        }

        private static string CreatePasswordHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        private static bool VerifyPasswordHash(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            catch
            {
                return false;
            }
        }
    }
}