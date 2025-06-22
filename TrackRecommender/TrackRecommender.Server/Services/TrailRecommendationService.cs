using TrackRecommender.Server.Models;
using TrackRecommender.Server.Models.DTOs;
using TrackRecommender.Server.Repositories.Interfaces;
using TrackRecommender.Server.Mappers.Implementations;

namespace TrackRecommender.Server.Services
{
    public class TrailRecommendationService(
        ITrailRepository trailRepository,
        IReviewRepository reviewRepository,
        IUserRepository userRepository,
        WeatherService weatherService,
        TrailMapper trailMapper)
    {
        private readonly ITrailRepository _trailRepository = trailRepository;
        private readonly IReviewRepository _reviewRepository = reviewRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly WeatherService _weatherService = weatherService;
        private readonly TrailMapper _trailMapper = trailMapper;

        private const double WEIGHT_DIFFICULTY = 0.20;
        private const double WEIGHT_TRAIL_TYPE = 0.20;
        private const double WEIGHT_DISTANCE = 0.10;
        private const double WEIGHT_DURATION = 0.10;
        private const double WEIGHT_RATING = 0.25;
        private const double WEIGHT_WEATHER = 0.15;

        public async Task<List<TrailRecommendationDto>> GetRecommendationsAsync(int userId, int count = 10, bool includeWeather = true)
        {
            var userPreferences = await _userRepository.GetUserPreferencesAsync(userId);
            if (userPreferences == null)
            {
                return await GetTopRatedTrailsAsync(count);
            }

            var allTrails = await _trailRepository.GetAllTrailsAsync();

            var scoredTrails = new List<(Trail trail, double score, Dictionary<string, double> scoreBreakdown)>();

            foreach (var trail in allTrails)
            {
                var scoreBreakdown = await CalculateTrailScoresAsync(trail, userPreferences, includeWeather);
                var totalScore = scoreBreakdown.Values.Sum();

                scoredTrails.Add((trail, totalScore, scoreBreakdown));
            }

            var topTrails = scoredTrails
                .OrderByDescending(x => x.score)
                .Take(count)
                .ToList();

            var recommendations = new List<TrailRecommendationDto>();

            foreach (var (trail, score, scoreBreakdown) in topTrails)
            {
                var dto = await _trailMapper.ToDtoAsync(trail);
                var recommendation = new TrailRecommendationDto
                {
                    Trail = dto,
                    RecommendationScore = score * 100,
                    ScoreBreakdown = scoreBreakdown.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value * 100
                    ),
                    MatchReasons = GenerateMatchReasons(userPreferences, scoreBreakdown)
                };

                recommendations.Add(recommendation);
            }

            return recommendations;
        }

        private async Task<Dictionary<string, double>> CalculateTrailScoresAsync(
            Trail trail,
            UserPreferences preferences,
            bool includeWeather)
        {
            var scores = new Dictionary<string, double>
            {
                ["difficulty"] = CalculateDifficultyScore(trail.Difficulty, preferences.PreferredDifficulty)
                                 * WEIGHT_DIFFICULTY,

                ["trailType"] = CalculateTrailTypeScore(trail.TrailType, preferences.PreferredTrailTypes)
                                 * WEIGHT_TRAIL_TYPE,

                ["distance"] = CalculateDistanceScore(trail.Distance, preferences.MaxDistance)
                               * WEIGHT_DISTANCE,

                ["duration"] = CalculateDurationScore(trail.Duration, preferences.MaxDuration)
                               * WEIGHT_DURATION,
            };

            var (averageRating, reviewCount) = await GetTrailRatingStatsAsync(trail.Id);
            scores["rating"] = CalculateRatingScore(averageRating, reviewCount, preferences.MinimumRating)
                             * WEIGHT_RATING;

            if (includeWeather)
            {
                scores["weather"] = await CalculateWeatherScoreAsync(trail) * WEIGHT_WEATHER;
            }
            else
            {
                var redistribution = WEIGHT_WEATHER / 5;
                scores["difficulty"] += scores["difficulty"] * (redistribution / WEIGHT_DIFFICULTY);
                scores["trailType"] += scores["trailType"] * (redistribution / WEIGHT_TRAIL_TYPE);
                scores["distance"] += scores["distance"] * (redistribution / WEIGHT_DISTANCE);
                scores["duration"] += scores["duration"] * (redistribution / WEIGHT_DURATION);
                scores["rating"] += scores["rating"] * (redistribution / WEIGHT_RATING);
            }

            return scores;
        }

        private static double CalculateDifficultyScore(string trailDifficulty, string preferredDifficulty)
        {
            var difficulties = new[] { "Easy", "Moderate", "Difficult", "Very Difficult", "Expert" };
            var trailIndex = Array.IndexOf(difficulties, trailDifficulty);
            var preferredIndex = Array.IndexOf(difficulties, preferredDifficulty);

            if (trailIndex == -1 || preferredIndex == -1) return 0.5;

            var difference = Math.Abs(trailIndex - preferredIndex);

            return difference switch
            {
                0 => 1.0,
                1 => 0.7,
                2 => 0.4,
                _ => 0.2
            };
        }

        private static double CalculateTrailTypeScore(string trailType, List<string> preferredTypes)
        {
            if (preferredTypes == null || preferredTypes.Count == 0)
                return 0.5;

            return preferredTypes.Contains(trailType) ? 1.0 : 0.2;
        }

        private static double CalculateDistanceScore(double trailDistance, double maxDistance)
        {
            if (maxDistance <= 0) return 0.5;

            if (trailDistance <= maxDistance)
            {
                return 0.5 + (trailDistance / maxDistance) * 0.5;
            }
            else
            {
                var ratio = maxDistance / trailDistance;
                return Math.Max(0.1, ratio * 0.5);
            }
        }

        private static double CalculateDurationScore(double trailDuration, double maxDuration)
        {
            if (maxDuration <= 0) return 0.5;

            if (trailDuration <= maxDuration)
            {
                return 0.5 + (trailDuration / maxDuration) * 0.5;
            }
            else
            {
                var ratio = maxDuration / trailDuration;
                return Math.Max(0.1, ratio * 0.5);
            }
        }

        private static double CalculateRatingScore(double averageRating, int reviewCount, int minimumRating)
        {
            if (reviewCount == 0)
                return 0.3;

            var normalizedRating = (averageRating - 1) / 4.0;

            var confidenceFactor = Math.Min(1.0, reviewCount / 10.0);

            if (averageRating < minimumRating)
                return normalizedRating * 0.3 * confidenceFactor;

            return normalizedRating * confidenceFactor;
        }

        private async Task<double> CalculateWeatherScoreAsync(Trail trail)
        {
            try
            {
                if (trail.Coordinates == null) return 0.5;

                var centroid = trail.Coordinates.Centroid;

                var weather = await _weatherService.GetWeatherByCoordinatesAsync(
                    centroid.Y,
                    centroid.X
                );

                if (weather == null) return 0.5;

                var currentScore = CalculateCurrentWeatherScore(weather.Current);

                var forecastScore = 0.0;
                var daysToCheck = Math.Min(3, weather.Daily.Count);

                if (daysToCheck > 0)
                {
                    for (int i = 0; i < daysToCheck; i++)
                    {
                        var dailyScore = CalculateDailyWeatherScore(weather.Daily[i]);
                        forecastScore += dailyScore * (1.0 - (i * 0.2));
                    }
                    forecastScore /= daysToCheck;
                }

                return currentScore * 0.7 + forecastScore * 0.3;
            }
            catch
            {
                return 0.5;
            }
        }

        private static double CalculateCurrentWeatherScore(WeatherCurrentDto current)
        {
            var score = 1.0;

            if (current.Weather.Any(w => w.Main == "Rain"))
                score *= 0.3;
            else if (current.Weather.Any(w => w.Main == "Snow"))
                score *= 0.2;
            else if (current.Weather.Any(w => w.Main == "Thunderstorm"))
                score *= 0.1;
            else if (current.Weather.Any(w => w.Main == "Drizzle"))
                score *= 0.5;
            else if (current.Clouds > 80)
                score *= 0.8;

            if (current.WindSpeed > 50)
                score *= 0.5;
            else if (current.WindSpeed > 30)
                score *= 0.7;

            if (current.Temp < 0 || current.Temp > 35)
                score *= 0.5;
            else if (current.Temp < 5 || current.Temp > 30)
                score *= 0.7;

            return score;
        }

        private static double CalculateDailyWeatherScore(WeatherDailyDto daily)
        {
            var score = 1.0;

            if (daily.Weather.Any(w => w.Main == "Rain"))
                score *= 0.3;
            else if (daily.Weather.Any(w => w.Main == "Snow"))
                score *= 0.2;
            else if (daily.Weather.Any(w => w.Main == "Thunderstorm"))
                score *= 0.1;
            else if (daily.Weather.Any(w => w.Main == "Drizzle"))
                score *= 0.5;

            if (daily.WindSpeed > 50)
                score *= 0.5;
            else if (daily.WindSpeed > 30)
                score *= 0.7;

            var avgTemp = daily.Temp.Day;
            if (avgTemp < 0 || avgTemp > 35)
                score *= 0.5;
            else if (avgTemp < 5 || avgTemp > 30)
                score *= 0.7;

            return score;
        }

        private async Task<(double averageRating, int reviewCount)> GetTrailRatingStatsAsync(int trailId)
        {
            var averageRating = await _reviewRepository.GetTrailAverageRatingAsync(trailId);
            var reviewCount = await _reviewRepository.GetTrailReviewCountAsync(trailId);

            return (averageRating, reviewCount);
        }

        private static List<string> GenerateMatchReasons(
            UserPreferences preferences,
            Dictionary<string, double> scoreBreakdown)
        {
            var reasons = new List<string>();

            foreach (var (component, score) in scoreBreakdown)
            {
                var percentage = score * 100;

                switch (component)
                {
                    case "difficulty" when percentage >= 70:
                        reasons.Add($"Matches your preferred difficulty level ({preferences.PreferredDifficulty})");
                        break;

                    case "trailType" when percentage >= 70:
                        reasons.Add($"Trail type matches your preferences");
                        break;

                    case "distance" when percentage >= 60:
                        reasons.Add($"Distance within your preferred range (max {preferences.MaxDistance} km)");
                        break;

                    case "duration" when percentage >= 60:
                        reasons.Add($"Duration within your preferred range (max {preferences.MaxDuration} hours)");
                        break;

                    case "rating" when percentage >= 60:
                        reasons.Add("Highly rated by other users");
                        break;

                    case "weather" when percentage >= 70:
                        reasons.Add("Good weather conditions for hiking");
                        break;
                }
            }

            if (reasons.Count == 0)
            {
                reasons.Add("Recommended based on your preferences");
            }

            return reasons;
        }

        private async Task<List<TrailRecommendationDto>> GetTopRatedTrailsAsync(int count)
        {
            var allTrails = await _trailRepository.GetAllTrailsAsync();
            var trailsWithRatings = new List<(Trail trail, double rating, int reviewCount)>();

            foreach (var trail in allTrails)
            {
                var (rating, reviewCount) = await GetTrailRatingStatsAsync(trail.Id);
                if (reviewCount > 0)
                {
                    trailsWithRatings.Add((trail, rating, reviewCount));
                }
            }

            var topRatedTrails = trailsWithRatings
                .OrderByDescending(x => x.rating)
                .ThenByDescending(x => x.reviewCount)
                .Take(count)
                .ToList();

            var recommendations = new List<TrailRecommendationDto>();

            foreach (var (trail, rating, reviewCount) in topRatedTrails)
            {
                var dto = await _trailMapper.ToDtoAsync(trail);
                var recommendation = new TrailRecommendationDto
                {
                    Trail = dto,
                    RecommendationScore = rating * 20,
                    ScoreBreakdown = new Dictionary<string, double>
                    {
                        { "rating", rating * 20 }
                    },
                    MatchReasons =
                    [
                        $"Average rating: {rating:F1}/5 from {reviewCount} reviews"
                    ]
                };

                recommendations.Add(recommendation);
            }

            return recommendations;
        }
    }
}