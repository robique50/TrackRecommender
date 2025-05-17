using System.Text;
using System.Text.Json;
using TrackRecommender.Server.Models;
using TrackRecommender.Server.Repositories.Interfaces;
using static TrackRecommender.Server.External.OpenStreetMap.OverpassModels;

namespace TrackRecommender.Server.Services
{
    public class TrailImportService
    {
        private readonly ITrailRepository _trailRepository;
        private readonly IRegionRepository _regionRepository;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TrailImportService> _logger;

        private int _trailCounter = 0;
        private const int OVERPASS_TIMEOUT_SECONDS = 300;
        private const int OVERPASS_DETAILED_FETCH_BATCH_SIZE = 10;
        private const int OVERPASS_API_DELAY_MS = 1500;
        private const int SAVE_CHANGES_BATCH_DELAY_MS = 1000;

        public TrailImportService(
            ITrailRepository trailRepository,
            IRegionRepository regionRepository,
            HttpClient httpClient,
            ILogger<TrailImportService> logger)
        {
            _trailRepository = trailRepository ?? throw new ArgumentNullException(nameof(trailRepository));
            _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task ImportAllTrailsAsync()
        {
            _logger.LogInformation("Starting trail import process...");
            _trailCounter = 0;

            try
            {
                var regions = await _regionRepository.GetAllRegionsAsync();
                if (!regions.Any())
                {
                    _logger.LogWarning("No regions found in the database. It's recommended to have regions pre-populated or ensure they are created correctly during import.");
                }

                string nationalInternationalCriteria = @"relation(area.romania)[""network""~""^(nwn|ncn|iwn|icn)$""][""route""~""^(hiking|foot|bicycle|mtb)$""];";
                await ImportTrailsByCriteriaAsync("National & International", nationalInternationalCriteria);

                string regionalCriteria = @"relation(area.romania)[""network""~""^(rwn|rcn)$""][""route""~""^(hiking|foot|bicycle|mtb)$""];";
                await ImportTrailsByCriteriaAsync("Regional", regionalCriteria);

                string localCriteria = @"
                (
                  relation(area.romania)[""network""~""^(lwn|lcn)$""][""route""~""^(hiking|foot|bicycle|mtb)$""];
                  relation(area.romania)[""route""~""^(hiking|foot|bicycle|mtb)$""][""name""][!""network""];
                );";
                await ImportTrailsByCriteriaAsync("Local", localCriteria);

                _logger.LogInformation($"Trail import process completed successfully. Total trails imported: {_trailCounter}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the overall trail import process");
            }
            finally
            {
                GC.Collect();
            }
        }

        private async Task ImportTrailsByCriteriaAsync(string categoryName, string overpassRelationCriteria)
        {
            _logger.LogInformation("Starting import of {CategoryName} trails...", categoryName);

            string idQuery = $@"
                [out:json][timeout:{OVERPASS_TIMEOUT_SECONDS}];
                area[""name""=""România""][""admin_level""=""2""]->.romania;
                {overpassRelationCriteria}
                out ids;";

            var idResponse = await ExecuteOverpassQueryAsync(idQuery);
            if (idResponse?.Elements == null || !idResponse.Elements.Any())
            {
                _logger.LogWarning("No {CategoryName} trails found in OSM data based on initial ID query.", categoryName);
                return;
            }

            var relationIds = idResponse.Elements.Where(e => e.Type == "relation").Select(e => e.Id).Distinct().ToList();
            _logger.LogInformation("Found {Count} {CategoryName} trail relation IDs. Fetching details sequentially...", relationIds.Count, categoryName);

            List<OverpassElement> fullyDetailedElements = new List<OverpassElement>();
            int conceptualBatchNumber = 0;

            for (int k = 0; k < relationIds.Count; k++)
            {
                var relationId = relationIds[k];

                if (k % OVERPASS_DETAILED_FETCH_BATCH_SIZE == 0)
                {
                    conceptualBatchNumber = (k / OVERPASS_DETAILED_FETCH_BATCH_SIZE) + 1;
                    _logger.LogInformation("Starting to fetch details for conceptual batch {BatchNum}/{TotalConceptualBatches} for {CategoryName} trails. Current relation ID: {RelationId}",
                        conceptualBatchNumber,
                        (int)Math.Ceiling((double)relationIds.Count / OVERPASS_DETAILED_FETCH_BATCH_SIZE),
                        categoryName,
                        relationId);
                }

                string detailedQuery = $@"
                    [out:json][timeout:{OVERPASS_TIMEOUT_SECONDS}];
                    relation({relationId});
                    (._;>;);
                    out body geom;";

                var detailedData = await ExecuteOverpassQueryAsync(detailedQuery);

                if (detailedData?.Elements != null && detailedData.Elements.Any())
                {
                    var relationElement = detailedData.Elements.FirstOrDefault(e => e.Type == "relation" && e.Id == relationId);
                    if (relationElement != null)
                    {
                        if ((relationElement.Geometry == null || !relationElement.Geometry.Any()) && relationElement.Members != null)
                        {
                            List<GeometryPoint> combinedGeometry = new List<GeometryPoint>();
                            foreach (var member in relationElement.Members.Where(m => m.Type == "way"))
                            {
                                var way = detailedData.Elements.FirstOrDefault(e => e.Type == "way" && e.Id == member.Ref);
                                if (way == null)
                                {
                                    _logger.LogWarning("Way member with ref {WayRefId} for relation {RelationId} not found in detailed OSM data.", member.Ref, relationId);
                                    continue;
                                }
                                if (way.Geometry == null || !way.Geometry.Any())
                                {
                                    _logger.LogWarning("Way member {WayId} (ref {WayRefId}) for relation {RelationId} has no geometry.", way.Id, member.Ref, relationId);
                                    continue;
                                }
                                combinedGeometry.AddRange(way.Geometry);
                            }

                            if (combinedGeometry.Any())
                            {
                                relationElement.Geometry = combinedGeometry;
                                _logger.LogInformation("Successfully built geometry for relation {RelationId} from {PointCount} points from its member ways.", relationId, combinedGeometry.Count);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to build geometry for relation {RelationId} from its members. It will likely be skipped.", relationId);
                            }
                        }
                        else if (relationElement.Geometry != null && relationElement.Geometry.Any())
                        {
                            _logger.LogInformation("Relation {RelationId} already has geometry with {PointCount} points.", relationId, relationElement.Geometry.Count);
                        }

                        if (relationElement.Geometry != null && relationElement.Geometry.Any())
                        {
                            fullyDetailedElements.Add(relationElement);
                        }
                        else
                        {
                            _logger.LogWarning("Relation {RelationId} processed but ended up with no usable geometry. Skipping.", relationId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find main relation element for ID {RelationId} in its detailed response.", relationId);
                    }
                }
                else
                {
                    _logger.LogWarning("No detailed data returned or no elements for relation ID {RelationId} (ExecuteOverpassQueryAsync might have returned null due to errors/retries failing).", relationId);
                }

                await Task.Delay(OVERPASS_API_DELAY_MS);
            }

            _logger.LogInformation("Finished fetching details for {CategoryName} trails. Total elements with geometry: {Count}.", categoryName, fullyDetailedElements.Count);

            int processingBatchSize = 50;
            for (int i = 0; i < fullyDetailedElements.Count; i += processingBatchSize)
            {
                var batchToProcess = fullyDetailedElements.Skip(i).Take(processingBatchSize).ToList();
                await ProcessTrailBatchAsync(batchToProcess);

                int savedCount = await _trailRepository.SaveChangesAsync();
                _logger.LogInformation("Saved {SavedCount} changes to the database after processing a batch of {CategoryName} trails.", savedCount, categoryName);

                if (i + processingBatchSize < fullyDetailedElements.Count)
                {
                    await Task.Delay(SAVE_CHANGES_BATCH_DELAY_MS);
                }
            }
            _logger.LogInformation("{CategoryName} trails import section completed.", categoryName);
        }

        public async Task<OverpassResponse?> ExecuteOverpassQueryAsync(string overpassQuery)
        {
            _logger.LogDebug("Executing Overpass query: {Query}", overpassQuery);
            int maxRetries = 3;
            int currentRetry = 0;
            TimeSpan retryDelay = TimeSpan.FromSeconds(15);

            while (true)
            {
                try
                {
                    var content = new StringContent($"data={Uri.EscapeDataString(overpassQuery)}", Encoding.UTF8, "application/x-www-form-urlencoded");
                    _logger.LogInformation("Sending Overpass API request... (Attempt {AttemptNum}) for query: {QueryType}",
                        currentRetry + 1,
                        overpassQuery.Contains("is_in") ? "IS_IN" : (overpassQuery.Contains("out ids") ? "OUT_IDS" : "RELATION_DETAILS"));
                    var response = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);

                    if (response.IsSuccessStatusCode)
                    {
                        return JsonSerializer.Deserialize<OverpassResponse>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || // 429
                             response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||   // 504
                             response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || // 503
                             response.StatusCode == System.Net.HttpStatusCode.BadGateway)        // 502
                    {
                        currentRetry++;
                        var errorBody = await response.Content.ReadAsStringAsync();
                        if (currentRetry > maxRetries)
                        {
                            _logger.LogError("Overpass API error ({StatusCode}). Max retries ({MaxRetries}) reached. Giving up. Body: {ErrorBody}. Query: {Query}",
                                (int)response.StatusCode, maxRetries, errorBody, overpassQuery);
                            return null;
                        }
                        _logger.LogWarning("Overpass API error ({StatusCode}) received. Retrying in {RetryDelaySeconds}s... (Attempt {CurrentRetry}/{MaxRetries}). Body: {ErrorBody}. Query: {Query}",
                            (int)response.StatusCode, retryDelay.TotalSeconds, currentRetry, maxRetries, errorBody, overpassQuery);

                        await Task.Delay(retryDelay);
                        retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2 + new Random().Next(1, 10001) / 1000.0);
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Overpass API error (non-retryable): {StatusCode}. Content: {ErrorContent}. Query: {Query}", response.StatusCode, errorContent, overpassQuery);
                        return null;
                    }
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Overpass query timed out or was canceled by HttpClient. Attempt {CurrentRetryVal}/{MaxRetries}. Query: {Query}", currentRetry + 1, maxRetries, overpassQuery);
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        _logger.LogError("Max retries reached for timeout/canceled query. Giving up. Query: {Query}", overpassQuery);
                        return null;
                    }
                    var timeoutRetryDelay = TimeSpan.FromSeconds(Math.Max(retryDelay.TotalSeconds, 20));
                    _logger.LogWarning("Waiting for {RetryDelaySeconds}s before retrying due to timeout/cancellation.", timeoutRetryDelay.TotalSeconds);
                    await Task.Delay(timeoutRetryDelay);
                    retryDelay = TimeSpan.FromSeconds(timeoutRetryDelay.TotalSeconds * 1.5 + new Random().Next(1, 5001) / 1000.0); // Următoarea pauză
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "HttpRequestException during Overpass query (Network issue?). Attempt {CurrentRetryVal}/{MaxRetries}. Query: {Query}", currentRetry + 1, maxRetries, overpassQuery);
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        _logger.LogError("Max retries reached for HttpRequestException. Giving up. Query: {Query}", overpassQuery);
                        return null;
                    }
                    var networkErrorRetryDelay = TimeSpan.FromSeconds(Math.Max(retryDelay.TotalSeconds, 15)); // Minim 15s pentru erori de rețea
                    _logger.LogWarning("Waiting for {RetryDelaySeconds}s before retrying due to HttpRequestException.", networkErrorRetryDelay.TotalSeconds);
                    await Task.Delay(networkErrorRetryDelay);
                    retryDelay = TimeSpan.FromSeconds(networkErrorRetryDelay.TotalSeconds * 1.5 + new Random().Next(1, 5001) / 1000.0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Generic exception during Overpass query. Attempt {CurrentRetryVal}/{MaxRetries}. Query: {Query}", currentRetry + 1, maxRetries, overpassQuery);
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        _logger.LogError("Max retries reached for generic exception. Giving up. Query: {Query}", overpassQuery);
                        return null;
                    }
                    var genericErrorRetryDelay = TimeSpan.FromSeconds(Math.Max(retryDelay.TotalSeconds, 10));
                    _logger.LogWarning("Waiting for {RetryDelaySeconds}s before retrying due to generic exception.", genericErrorRetryDelay.TotalSeconds);
                    await Task.Delay(genericErrorRetryDelay);
                    retryDelay = TimeSpan.FromSeconds(genericErrorRetryDelay.TotalSeconds * 1.5 + new Random().Next(1, 5001) / 1000.0);
                }
            }
        }

        private async Task ProcessTrailBatchAsync(List<OverpassElement> trails)
        {
            if (!trails.Any()) return;
            _logger.LogInformation("Processing batch of {TrailCount} trails for database insertion.", trails.Count);

            foreach (var element in trails)
            {
                try
                {
                    await ProcessSingleTrailAsync(element);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error processing single trail with OSM ID {OsmId}. Skipping this trail.", element.Id);
                }
            }
        }

        private async Task ProcessSingleTrailAsync(OverpassElement element)
        {
            if (element.Tags == null || !element.Tags.Any())
            {
                _logger.LogWarning("Skipping trail with OSM ID {OsmId} - missing tags.", element.Id);
                return;
            }

            if (element.Geometry == null || !element.Geometry.Any())
            {
                _logger.LogWarning("Skipping trail with OSM ID {OsmId} - missing or empty geometry after fetch and construction attempts.", element.Id);
                return;
            }

            if (!element.Tags.TryGetValue("name", out var trailName) || string.IsNullOrWhiteSpace(trailName))
            {
                _logger.LogWarning("Skipping trail with OSM ID {OsmId} - missing name tag.", element.Id);
                return;
            }

            if (IsNonRomanianTrail(trailName))
            {
                _logger.LogInformation("Skipping non-Romanian trail based on name: {TrailName} (OSM ID: {OsmId})", trailName, element.Id);
                return;
            }

            var coordinates = element.Geometry.Select(g => new[] { g.Lon, g.Lat }).ToList();
            double distance = CalculateDistance(coordinates);

            if (distance < 3)
            {
                _logger.LogInformation("Skipping trail '{TrailName}' (OSM ID: {OsmId}) - too short ({DistanceKm:F2} km).", trailName, element.Id, distance);
                return;
            }

            var intersectedRegionIds = await DetermineRegionsForTrailAsync(coordinates, trailName, element.Id);
            if (!intersectedRegionIds.Any())
            {
                _logger.LogWarning("Skipping trail '{TrailName}' (OSM ID: {OsmId}) - does not intersect any known or creatable regions.", trailName, element.Id);
                return;
            }

            string geoJsonData = JsonSerializer.Serialize(new { type = "LineString", coordinates });
            string network = element.Tags.TryGetValue("network", out var networkValue) ? networkValue : string.Empty;
            string category = DetermineCategoryFromNetwork(network);

            if (await _trailRepository.TrailExistsAsync(trailName, network, geoJsonData))
            {
                _logger.LogInformation("Trail '{TrailName}' (Network: {Network}, OSM ID: {OsmId}) already exists with identical geometry. Skipping.", trailName, network, element.Id);
                return;
            }

            var regions = new List<Region>();
            var regionNames = new List<string>();
            foreach (var regionId in intersectedRegionIds)
            {
                var region = await _regionRepository.GetRegionByIdAsync(regionId);
                if (region != null)
                {
                    regions.Add(region);
                    regionNames.Add(region.Name);
                }
                else
                {
                    _logger.LogWarning("Could not retrieve region with ID {RegionId} for trail '{TrailName}' (OSM ID: {OsmId}) even after attempting to determine/create it.", regionId, trailName, element.Id);
                }
            }
            if (!regions.Any())
            {
                _logger.LogWarning("Trail '{TrailName}' (OSM ID: {OsmId}) ended up with no valid regions after lookups. Skipping.", trailName, element.Id);
                return;
            }

            string difficultyForTrail = DetermineDifficulty(element.Tags, distance);
            string trailTypeForTrail = DetermineTrailTypeFromOsm(element.Tags);

            var trail = new Trail
            {
                Name = trailName,
                Description = element.Tags.TryGetValue("description", out var desc) ? desc : $"Traseu {category.ToLower()} în {string.Join(", ", regionNames)}.",
                Distance = distance,
                Difficulty = difficultyForTrail,
                TrailType = trailTypeForTrail,
                Duration = EstimateDuration(distance, difficultyForTrail, trailTypeForTrail, element.Tags),
                StartLocation = coordinates.Any() ? $"{coordinates.First()[1]:F6},{coordinates.First()[0]:F6}" : string.Empty,
                EndLocation = coordinates.Count > 1 ? $"{coordinates.Last()[1]:F6},{coordinates.Last()[0]:F6}" : string.Empty,
                GeoJsonData = geoJsonData,
                Tags = ExtractRelevantTags(element.Tags),
                Category = category,
                Network = network,
                Regions = regions,
            };

            await _trailRepository.AddTrailAsync(trail);
            _trailCounter++;
            _logger.LogInformation("Successfully prepared trail '{TrailName}' (OSM ID: {OsmId}) for addition. Category: {Category}, Distance: {DistanceKm:F2} km, Regions: {RegionNames}",
                trailName, element.Id, category, distance, string.Join(", ", regionNames));
        }


        private async Task<List<int>> DetermineRegionsForTrailAsync(List<double[]> coordinates, string trailNameForLogging, long osmIdForLogging)
        {
            if (coordinates == null || !coordinates.Any())
            {
                _logger.LogWarning("Cannot determine regions for trail '{TrailName}' (OSM ID: {OsmIdForLogging}): no coordinates provided.", trailNameForLogging, osmIdForLogging);
                return new List<int>();
            }

            var checkPoints = new List<double[]>();
            if (coordinates.Any()) checkPoints.Add(coordinates.First());
            if (coordinates.Count > 1) checkPoints.Add(coordinates[coordinates.Count / 2]);
            if (coordinates.Count > 2) checkPoints.Add(coordinates.Last());

            checkPoints = checkPoints.GroupBy(p => $"{p[0]},{p[1]}").Select(g => g.First()).ToList();

            var intersectedRegionIds = new HashSet<int>();

            foreach (var point in checkPoints)
            {
                string regionQuery = $@"
                    [out:json][timeout:90];
                    is_in({point[1]:F6},{point[0]:F6});
                    area._[admin_level=""4""][boundary=""administrative""];
                    out tags;";

                var regionData = await ExecuteOverpassQueryAsync(regionQuery);
                if (regionData?.Elements != null)
                {
                    foreach (var areaElement in regionData.Elements.Where(e => e.Type == "area"))
                    {
                        if (areaElement.Tags != null && areaElement.Tags.TryGetValue("name", out var regionName))
                        {
                            var region = await _regionRepository.GetRegionByNameAsync(regionName);
                            if (region == null)
                            {
                                _logger.LogInformation("Region '{RegionName}' not found in DB for trail '{TrailName}' (OSM ID: {OsmIdForLogging}). Creating it.", regionName, trailNameForLogging, osmIdForLogging);
                                var newRegion = new Region { Name = regionName };
                                await _regionRepository.AddRegionAsync(newRegion);
                                await _regionRepository.SaveChangesAsync();
                                region = await _regionRepository.GetRegionByNameAsync(regionName);
                            }

                            if (region != null)
                            {
                                intersectedRegionIds.Add(region.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to find or create region '{RegionName}' for trail '{TrailName}' (OSM ID: {OsmIdForLogging}).", regionName, trailNameForLogging, osmIdForLogging);
                            }
                        }
                    }
                }
                await Task.Delay(OVERPASS_API_DELAY_MS);
            }

            if (!intersectedRegionIds.Any())
            {
                _logger.LogWarning("No regions determined for trail '{TrailName}' (OSM ID: {OsmIdForLogging}) after checking {PointCount} points. The trail might be outside known admin boundaries or too short.",
                    trailNameForLogging, osmIdForLogging, checkPoints.Count);
            }
            return intersectedRegionIds.ToList();
        }

        private double CalculateDistance(List<double[]> coordinates)
        {
            if (coordinates == null || coordinates.Count < 2) return 0;
            double totalDistance = 0;
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                totalDistance += CalculateHaversineDistance(
                    coordinates[i][1], coordinates[i][0],
                    coordinates[i + 1][1], coordinates[i + 1][0]
                );
            }
            return Math.Round(totalDistance, 2);
        }
        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371.0;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }
        private double ToRadians(double degrees) => degrees * (Math.PI / 180.0);

        private string DetermineCategoryFromNetwork(string? network)
        {
            if (string.IsNullOrEmpty(network)) return "Local";
            network = network.ToLowerInvariant();
            if (network.Contains("iwn") || network.Contains("icn")) return "International";
            if (network.Contains("nwn") || network.Contains("ncn")) return "National";
            if (network.Contains("rwn") || network.Contains("rcn")) return "Regional";
            return "Local";
        }
        private bool IsNonRomanianTrail(string name)
        {
            if (name.Any(c => "öüäßçğøåæłńśźżščřďťňÖÜÄẞÇĞØÅÆŁŃŚŹŻŠČŘĎŤŇ".Contains(c)))
            {
                if (!name.Any(c => "ăâîșțĂÂÎȘȚ".Contains(c))) return true;
            }
            return false;
        }
        private string DetermineDifficulty(Dictionary<string, string> tags, double distance)
        {
            if (tags.TryGetValue("sac_scale", out var sacScale))
            {
                return sacScale.ToLowerInvariant() switch
                {
                    "hiking" => "Easy",
                    "mountain_hiking" => "Moderate",
                    "alpine_hiking" => "Difficult",
                    _ => "Moderate"
                };
            }

            if (tags.TryGetValue("difficulty", out var osmDiff)) return osmDiff;
            if (distance < 5) return "Easy";
            if (distance < 12) return "Moderate";
            if (distance < 20) return "Difficult";
            if (distance < 35) return "Very Difficult";
            return "Expert";
        }
        private string DetermineTrailTypeFromOsm(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("route", out var routeValue))
            {
                return routeValue.ToLowerInvariant() switch
                {
                    "hiking" => "Hiking",
                    "foot" => "Foot Trail",
                    "bicycle" => "Bicycle Trail",
                    "mtb" => "MTB Trail",
                    "horse" => "Equestrian Trail",
                    "ski" => "Ski Trail",
                    _ => routeValue
                };
            }
            if (tags.TryGetValue("highway", out var highwayValue))
            {
                return highwayValue.ToLowerInvariant() switch
                {
                    "path" => "Path",
                    "track" => "Track",
                    "footway" => "Footway",
                    "cycleway" => "Cycleway",
                    "bridleway" => "Bridleway",
                    "steps" => "Steps",
                    _ => highwayValue
                };
            }
            return "Unspecified Trail";
        }
        private double EstimateDuration(double distance, string difficulty, string trailType, Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("duration", out var durationStr))
            {
                if (TryParseOsmDuration(durationStr, out double osmHours))
                {
                    _logger.LogInformation("Using duration from OSM tag for trail (Distance: {Distance}km): OSM value '{OsmDurationStr}' -> Parsed to {OsmHours}h", distance, durationStr, osmHours);
                    return Math.Round(osmHours, 1);
                }
                else
                {
                    _logger.LogWarning("Failed to parse OSM duration tag: '{OsmDurationStr}'. Estimating duration based on distance, difficulty, and type.", durationStr);
                }
            }

            double speedKmh;

            bool isPrimarilyHiking = trailType.Contains("Hiking", StringComparison.OrdinalIgnoreCase) ||
                                     trailType.Contains("Foot Path", StringComparison.OrdinalIgnoreCase) ||
                                     trailType.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                                     trailType.Contains("Footway", StringComparison.OrdinalIgnoreCase);

            bool isPrimarilyBiking = trailType.Contains("Bicycle", StringComparison.OrdinalIgnoreCase) ||
                                     trailType.Contains("Cycleway", StringComparison.OrdinalIgnoreCase) ||
                                     trailType.Contains("MTB", StringComparison.OrdinalIgnoreCase);

            if (isPrimarilyHiking)
            {
                speedKmh = difficulty switch
                {
                    "Easy" => 4.0,
                    "Moderate" => 3.5,
                    "Difficult" => 2.8,
                    "Very Difficult" => 2.2,
                    "Expert" => 1.8,
                    _ => 3.0
                };
            }
            else if (isPrimarilyBiking)
            {
                speedKmh = difficulty switch
                {
                    "Easy" => 18.0,
                    "Moderate" => 15.0,
                    "Difficult" => 12.0,
                    "Very Difficult" => 9.0,
                    "Expert" => 7.0,
                    _ => 12.0
                };
            }
            else
            {
                speedKmh = 3.0;
            }

            if (distance <= 0 || speedKmh <= 0) return 0;

            double baseHours = distance / speedKmh;

            double breakFactor = isPrimarilyBiking ? 0.15 : 0.20;
            double totalHours = baseHours * (1 + breakFactor);

            _logger.LogInformation("Estimated duration for trail (Distance: {Distance}km, Difficulty: {Difficulty}, Type: {TrailType}): Speed {SpeedKmh}km/h, BaseHours {BaseHours:F1}h, TotalHours {TotalHours:F1}h",
                distance, difficulty, trailType, speedKmh, baseHours, totalHours);

            return Math.Round(totalHours, 1);
        }

        private bool TryParseOsmDuration(string durationStr, out double hours)
        {
            hours = 0;
            if (string.IsNullOrWhiteSpace(durationStr)) return false;

            try
            {
                if (durationStr.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
                {
                    hours = System.Xml.XmlConvert.ToTimeSpan(durationStr.ToUpperInvariant()).TotalHours;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not parse OSM duration string '{DurationStr}' as ISO 8601 TimeSpan.", durationStr);
            }

            var parts = durationStr.Split(':');
            if (parts.Length == 2 || parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                {
                    int s = 0;
                    if (parts.Length == 3 && !int.TryParse(parts[2], out s))
                    {
                        return false;
                    }

                    if (h >= 0 && m >= 0 && m < 60 && s >= 0 && s < 60)
                    {
                        hours = h + (m / 60.0) + (s / 3600.0);
                        return true;
                    }
                }
            }
            if (double.TryParse(durationStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double directHours))
            {
                if (directHours >= 0)
                {
                    hours = directHours;
                    return true;
                }
            }
            return false;
        }

        private List<string> ExtractRelevantTags(Dictionary<string, string> tags)
        {
            var relevantTags = new List<string>();
            if (tags == null || !tags.Any())
                return relevantTags;

            string[] usefulKeys = {
                "name", "name:ro", "alt_name", "official_name", "short_name",
                "ref", "network", "route", "operator", "description:ro", "website", "source",
                "note", "fixme", "wikidata", "wikipedia",

                "highway", "surface", "tracktype", "smoothness", "width", "incline",
                "mtb:scale", "mtb:scale:uphill", "mtb:scale:imba", "sac_scale", "trail_visibility",

                "osmc:symbol", "wiki:symbol", "symbol", "colour", "color",
                "cables", "handrail", "bridge", "tunnel", "ford",

                "access", "foot", "bicycle", "mtb", "horse", "ski", "motor_vehicle",
                "fee", "oneway", "seasonal", "winter_road", "opening_hours",

                "drinking_water", "shelter_type",

                "hazard", "warning",

                "lit", "importance", "layer", "covered"
            };

            foreach (var key in usefulKeys)
            {
                if (tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    relevantTags.Add($"{key}={value}");
                }
            }

            return relevantTags.Distinct().ToList();
        }
    }
}