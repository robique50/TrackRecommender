using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using TrackRecommender.Server.Models.DTOs;

namespace TrackRecommender.Server.Services
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

                var currentWeatherUrl = $"{_baseUrl}/weather?lat={roundedLat}&lon={roundedLon}&units=metric&appid={_apiKey}";
                var currentResponse = await _httpClient.GetAsync(currentWeatherUrl);
                currentResponse.EnsureSuccessStatusCode();
                var currentData = await JsonDocument.ParseAsync(await currentResponse.Content.ReadAsStreamAsync());

                var forecastUrl = $"{_baseUrl}/forecast?lat={roundedLat}&lon={roundedLon}&units=metric&appid={_apiKey}";
                var forecastResponse = await _httpClient.GetAsync(forecastUrl);
                forecastResponse.EnsureSuccessStatusCode();
                var forecastData = await JsonDocument.ParseAsync(await forecastResponse.Content.ReadAsStreamAsync());

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
                            currentData.RootElement.GetProperty("weather").GetRawText()) ?? []
                    },
                    Daily = ProcessForecastToDaily(forecastData)
                };

                _memoryCache.Set(cacheKey, response, _cacheDuration);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Weather API error: {ex.Message}");
                return null;
            }
        }

        private static List<WeatherDailyDto> ProcessForecastToDaily(JsonDocument forecastData)
        {
            var dailyForecasts = new Dictionary<string, WeatherDailyAggregation>();

            foreach (var forecast in forecastData.RootElement.GetProperty("list").EnumerateArray())
            {
                var dt = forecast.GetProperty("dt").GetInt64();
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(dt);
                var dateKey = dateTime.ToString("yyyy-MM-dd");

                if (!dailyForecasts.TryGetValue(dateKey, out WeatherDailyAggregation? value))
                {
                    value = new WeatherDailyAggregation
                    {
                        Date = dateTime.Date,
                        Dt = dt,
                        Temps = [],
                        Humidities = [],
                        WindSpeeds = [],
                        WeatherConditions = []
                    };
                    dailyForecasts[dateKey] = value;
                }

                var main = forecast.GetProperty("main");
                value.Temps.Add(main.GetProperty("temp").GetDouble());
                value.Humidities.Add(main.GetProperty("humidity").GetInt32());
                value.WindSpeeds.Add(forecast.GetProperty("wind").GetProperty("speed").GetDouble());

                if (value.WeatherConditions.Count == 0)
                {
                    var weather = JsonSerializer.Deserialize<List<WeatherConditionDto>>(
                        forecast.GetProperty("weather").GetRawText());
                    if (weather != null && weather.Count > 0)
                    {
                        dailyForecasts[dateKey].WeatherConditions.Add(weather[0]);
                    }
                }
            }

            var result = new List<WeatherDailyDto>();
            foreach (var daily in dailyForecasts.Values.OrderBy(d => d.Date).Take(5))
            {
                result.Add(new WeatherDailyDto
                {
                    Dt = daily.Dt,
                    Temp = new WeatherTempDto
                    {
                        Min = daily.Temps.Min(),
                        Max = daily.Temps.Max(),
                        Day = daily.Temps.Average(),
                        Night = daily.Temps.Take(daily.Temps.Count / 2).Average()
                    },
                    Humidity = (int)daily.Humidities.Average(),
                    WindSpeed = daily.WindSpeeds.Average(),
                    Weather = daily.WeatherConditions
                });
            }

            return result;
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
            catch (Exception ex)
            {
                Console.WriteLine($"Weather location error: {ex.Message}");
                return null;
            }
        }
    }
}