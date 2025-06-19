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
                    Current = ParseCurrentWeather(currentData.RootElement),
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

        private static WeatherCurrentDto ParseCurrentWeather(JsonElement root)
        {
            var current = new WeatherCurrentDto
            {
                Temp = root.GetProperty("main").GetProperty("temp").GetDouble(),
                FeelsLike = root.GetProperty("main").GetProperty("feels_like").GetDouble(),
                Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32(),
                WindSpeed = root.GetProperty("wind").GetProperty("speed").GetDouble(),
                Clouds = root.GetProperty("clouds").GetProperty("all").GetInt32(),
                Weather = []
            };

            if (root.TryGetProperty("weather", out var weatherArray) && weatherArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var weatherItem in weatherArray.EnumerateArray())
                {
                    current.Weather.Add(new WeatherConditionDto
                    {
                        Id = weatherItem.GetProperty("id").GetInt32(),
                        Main = weatherItem.GetProperty("main").GetString() ?? "",
                        Description = weatherItem.GetProperty("description").GetString() ?? "",
                        Icon = weatherItem.GetProperty("icon").GetString() ?? ""
                    });
                }
            }

            return current;
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

                if (forecast.TryGetProperty("weather", out var weatherArray) &&
                    weatherArray.ValueKind == JsonValueKind.Array &&
                    weatherArray.GetArrayLength() > 0)
                {
                    var firstWeather = weatherArray.EnumerateArray().First();
                    var weatherCondition = new WeatherConditionDto
                    {
                        Id = firstWeather.GetProperty("id").GetInt32(),
                        Main = firstWeather.GetProperty("main").GetString() ?? "",
                        Description = firstWeather.GetProperty("description").GetString() ?? "",
                        Icon = firstWeather.GetProperty("icon").GetString() ?? ""
                    };

                    if (weatherCondition.Icon.EndsWith('n'))
                    {
                        weatherCondition.Icon = weatherCondition.Icon.Replace("n", "d");
                    }

                    var hour = dateTime.Hour;
                    if (hour >= 6 && hour <= 18)
                    {
                        if (value.WeatherConditions.Count > 0 && value.WeatherConditions[0].Icon.EndsWith('n'))
                        {
                            value.WeatherConditions[0] = weatherCondition;
                        }
                        else if (value.WeatherConditions.Count == 0)
                        {
                            value.WeatherConditions.Add(weatherCondition);
                        }
                    }
                    else if (value.WeatherConditions.Count == 0)
                    {
                        value.WeatherConditions.Add(weatherCondition);
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
                    Weather = daily.WeatherConditions.Count != 0
                        ? daily.WeatherConditions
                        :
                        [
                            new() { Id = 0, Main = "", Description = "", Icon = "" }
                        ]
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

                var geocodingUrl = $"http://api.openweathermap.org/geo/1.0/direct?q={locationName}&limit=1&appid={_apiKey}";
                var geoResponse = await _httpClient.GetAsync(geocodingUrl);
                geoResponse.EnsureSuccessStatusCode();

                var geoData = await JsonDocument.ParseAsync(await geoResponse.Content.ReadAsStreamAsync());
                var geoArray = geoData.RootElement.EnumerateArray().ToList();

                if (geoArray.Count == 0)
                {
                    throw new InvalidOperationException($"Location '{locationName}' not found");
                }

                var location = geoArray.First();
                var lat = location.GetProperty("lat").GetDouble();
                var lon = location.GetProperty("lon").GetDouble();

                var weatherData = await GetWeatherByCoordinatesAsync(lat, lon);

                if (weatherData != null)
                {
                    weatherData.Location.Name = location.GetProperty("name").GetString() ?? locationName;
                    weatherData.Location.Country = location.GetProperty("country").GetString() ?? "Unknown";

                    _memoryCache.Set(cacheKey, weatherData, _cacheDuration);
                }

                return weatherData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Weather API error: {ex.Message}");
                return null;
            }
        }
    }
}