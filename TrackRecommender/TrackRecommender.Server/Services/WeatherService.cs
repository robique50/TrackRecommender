using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Services.Implementations
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        private readonly string? _apiKey;
        private readonly string _baseUrl;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public WeatherService(
            HttpClient httpClient,
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _memoryCache = memoryCache;

            _apiKey = _configuration["Weather:ApiKey"];
            _baseUrl = "https://api.openweathermap.org/data/2.5";
        }

        public async Task<WeatherResponseDto?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    throw new InvalidOperationException("Weather API key is not configured");
                }

                double roundedLat = Math.Round(latitude, 1);
                double roundedLon = Math.Round(longitude, 1);

                string cacheKey = $"weather_coord_{roundedLat}_{roundedLon}";
                if (_memoryCache.TryGetValue(cacheKey, out WeatherResponseDto? cachedResponse) && cachedResponse != null)
                {
                    return cachedResponse;
                }

                // Doar un singur request către API-ul care funcționează
                var currentWeatherUrl = $"{_baseUrl}/weather?lat={roundedLat}&lon={roundedLon}&units=metric&appid={_apiKey}";
                var currentResponse = await _httpClient.GetAsync(currentWeatherUrl);
                currentResponse.EnsureSuccessStatusCode();
                var currentData = await JsonDocument.ParseAsync(await currentResponse.Content.ReadAsStreamAsync());

                var response = new WeatherResponseDto
                {
                    Location = new WeatherLocationDto
                    {
                        Name = currentData.RootElement.GetProperty("name").GetString() ?? "Unknown",
                        Country = currentData.RootElement.GetProperty("sys").GetProperty("country").GetString() ?? "Unknown",
                        Lat = roundedLat,
                        Lon = roundedLon
                    },
                    Current = new WeatherCurrentDto
                    {
                        Temp = currentData.RootElement.GetProperty("main").GetProperty("temp").GetDouble(),
                        FeelsLike = currentData.RootElement.GetProperty("main").GetProperty("feels_like").GetDouble(),
                        Humidity = currentData.RootElement.GetProperty("main").GetProperty("humidity").GetInt32(),
                        WindSpeed = currentData.RootElement.GetProperty("wind").GetProperty("speed").GetDouble(),
                        Clouds = currentData.RootElement.GetProperty("clouds").GetProperty("all").GetInt32(),
                        Weather = JsonSerializer.Deserialize<List<WeatherConditionDto>>(
                            currentData.RootElement.GetProperty("weather").GetRawText()) ?? new List<WeatherConditionDto>()
                    },
                    Daily = new List<WeatherDailyDto>()
                };

                _memoryCache.Set(cacheKey, response, _cacheDuration);
                return response;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<WeatherResponseDto?> GetWeatherByLocationNameAsync(string locationName)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    throw new InvalidOperationException("Weather API key is not configured");
                }

                if (string.IsNullOrWhiteSpace(locationName))
                {
                    throw new ArgumentException("Location name cannot be empty", nameof(locationName));
                }

                string cacheKey = $"weather_location_{locationName.ToLowerInvariant()}";
                if (_memoryCache.TryGetValue(cacheKey, out WeatherResponseDto? cachedResponse) && cachedResponse != null)
                {
                    return cachedResponse;
                }

                var geoUrl = $"https://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(locationName)}&limit=1&appid={_apiKey}";
                var geoResponse = await _httpClient.GetAsync(geoUrl);
                geoResponse.EnsureSuccessStatusCode();

                var geoData = await JsonDocument.ParseAsync(await geoResponse.Content.ReadAsStreamAsync());
                if (geoData.RootElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException($"Location '{locationName}' not found");
                }

                var firstLocation = geoData.RootElement[0];
                double lat = firstLocation.GetProperty("lat").GetDouble();
                double lon = firstLocation.GetProperty("lon").GetDouble();

                var response = await GetWeatherByCoordinatesAsync(lat, lon);
                if (response == null)
                {
                    return null;
                }

                response.Location.Name = firstLocation.GetProperty("name").GetString() ?? locationName;
                if (firstLocation.TryGetProperty("country", out var countryElement))
                {
                    response.Location.Country = countryElement.GetString() ?? response.Location.Country;
                }

                _memoryCache.Set(cacheKey, response, _cacheDuration);

                return response;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}