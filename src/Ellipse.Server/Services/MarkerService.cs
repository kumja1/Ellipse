using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Directions;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

namespace Ellipse.Server.Services;

public class MarkerService(GeocodingService geocodingService, IDistributedCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _tasks = [];
    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<List<MarkerResponse?>> GetMarkers(BatchMarkerRequest request, bool overwriteCache)
    {
        string cacheKey = CacheHelper.CreateCacheKey(request.Points, request.Schools);
        if (!overwriteCache)
        {
            string? cachedData = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Log.Information("Cache hit for batch request: {RequestId}", cacheKey);
                List<MarkerResponse?> deserialized = JsonSerializer.Deserialize<List<MarkerResponse?>>(
                    CacheHelper.DecompressData(cachedData)
                )!;

                Log.Information("Returning cached MarkerResponse");
                return deserialized;
            }
        }

        List<MarkerResponse?> results = await GetMarkersInternal(request).ConfigureAwait(false);
        if (results.Count != 0)
            await cache.SetStringAsync(cacheKey, CacheHelper.CompressData(JsonSerializer.Serialize(results)));

        return results;
    }

    private async Task<List<MarkerResponse?>> GetMarkersInternal(BatchMarkerRequest request)
    {
        Dictionary<GeoPoint2d, MarkerResponse?> resultsMap = [];
        string[] addresses = await Task.WhenAll(
            request.Points.Select(p => geocodingService.GetAddressCached(p.Lon, p.Lat))
        );

        List<Dictionary<string, SchoolRoute>> allRoutes =
            await GetMatrixRoute(request.Points, request.Schools).ConfigureAwait(false);

        for (int i = 0; i < request.Points.Length; i++)
        {
            GeoPoint2d point = request.Points[i];
            Dictionary<string, SchoolRoute> routes = allRoutes[i];

            if (routes.Count == 0)
            {
                resultsMap[point] = null;
                Log.Warning("No routes found for point: {Point}", point);
                continue;
            }

            List<double> distances = [.. routes.Values.Select(r => r.Distance)];
            double avgDistance = Trimean(distances);
            double avgDuration = Trimean([.. routes.Values.Select(r => r.Duration.TotalSeconds)]);

            routes["Average"] = new SchoolRoute
            {
                Distance = avgDistance,
                Duration = TimeSpan.FromSeconds(avgDuration)
            };
            resultsMap[point] = new MarkerResponse(addresses[i], distances.Sum(), routes);
        }

        return request.Points.Select(p => resultsMap[p]).ToList();
    }

    public async Task<MarkerResponse?> GetMarker(MarkerRequest request, bool overwriteCache)
    {
        Log.Information("Called for point: {Point}", request.Point);


        string cacheKey = CacheHelper.CreateCacheKey("marker", request.Point);
        if (!overwriteCache)
        {
            string? cachedData = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Log.Information("Cache hit for point: {Point}", request.Point);
                MarkerResponse deserialized = JsonSerializer.Deserialize<MarkerResponse>(
                    CacheHelper.DecompressData(cachedData)
                )!;
                Log.Information("Returning cached MarkerResponse");
                return deserialized;
            }
        }

        Log.Information("Cache miss for point: {Point}", request.Point);
        try
        {
            MarkerResponse? markerResponse = await _tasks
                .GetOrAdd(request.Point, _ => GetMarkerInternal(request))
                .ConfigureAwait(false);

            if (markerResponse == null)
            {
                Log.Warning("MarkerResponse is null for point: {Point}", request.Point);
                return null;
            }

            await cache.SetStringAsync(
                cacheKey,
                CacheHelper.CompressData(JsonSerializer.Serialize(markerResponse))
            );

            Log.Information("Cached new MarkerResponse for point: {Point}", request.Point);
            return markerResponse;
        }
        finally
        {
            _tasks.TryRemove(request.Point, out _);
        }
    }

    private async Task<MarkerResponse?> GetMarkerInternal(MarkerRequest request)
    {
        Log.Information("Processing MarkerRequest for point: {Point}", request.Point);
        if (request.Schools.Length == 0)
        {
            Log.Warning("No schools provided. Returning null.");
            return null;
        }

        string address = await geocodingService
            .GetAddressCached(request.Point.Lon, request.Point.Lat)
            .ConfigureAwait(false);

        Log.Information("Address fetched: {Address}", address);
        List<Dictionary<string, SchoolRoute>> allRoutes =
            await GetMatrixRoute([request.Point], request.Schools).ConfigureAwait(false);
        Dictionary<string, SchoolRoute> routes = allRoutes[0];

        Log.Information("Matrix routes obtained. Count: {Count}", routes.Count);
        if (routes.Count == 0)
        {
            Log.Warning("No routes found. Returning null.");
            return null;
        }

        List<double> distances = [.. routes.Values.Select(r => r.Distance)];
        double avgDistance = Trimean(distances);
        double avgDuration = Trimean([.. routes.Values.Select(r => r.Duration.TotalSeconds)]);

        routes["Average"] = new SchoolRoute { Distance = avgDistance, Duration = TimeSpan.FromSeconds(avgDuration) };
        Log.Information(
            "Calculated average route: Distance={Distance}, Duration={Duration}",
            avgDistance,
            avgDuration
        );

        // MapillaryResponse<MapillaryImage> mapillaryResponse = await mapillaryClient.SearchImages(
        //     new MapillarySearchRequest
        //     {
        //         MaxLat = request.Point.Lat + 0.005,
        //         MaxLon = request.Point.Lon + 0.005,
        //         MinLat = request.Point.Lat - 0.005,
        //         MinLon = request.Point.Lon - 0.005,
        //         Limit = 10
        //     });

        // MapillaryImage image = mapillaryResponse.Data[0];
        return routes.Count == 0
            ? null
            : new MarkerResponse(address, distances.Sum(), routes);
    }


    private async Task<List<Dictionary<string, SchoolRoute>>> GetMatrixRoute(
        GeoPoint2d[] sources,
        SchoolData[] schools
    )
    {
        List<Dictionary<string, SchoolRoute>> results = Enumerable
            .Range(0, sources.Length)
            .Select(_ => new Dictionary<string, SchoolRoute>(schools.Length))
            .ToList();

        await Retry.Default(
            async attempt =>
            {
                Log.Information("Attempt {Retry} for {Count} schools", attempt, schools.Length);
                await _semaphore.WaitAsync();
                try
                {
                    (double[][] distances, double[][] durations) =
                        await geocodingService.GetMatrixCached(
                            sources,
                            [.. schools.Select(s => s.LatLng)]
                        );

                    for (int i = 0; i < sources.Length; i++)
                    {
                        Dictionary<string, SchoolRoute> currentDict = results[i];
                        for (int j = 0; j < schools.Length; j++)
                        {
                            SchoolData school = schools[j];
                            string schoolKey = $"{school.Name} ({school.Division})";
                            double distance = distances[i][j];
                            double duration = durations[i][j];


                            if (currentDict.ContainsKey(schoolKey))
                            {
                                Log.Warning("Duplicate school key: {Key}. Current point: {Point}", schoolKey,
                                    sources[i]);
                                continue;
                            }

                            currentDict.Add(
                                schoolKey,
                                new SchoolRoute
                                {
                                    Name = school.Name,
                                    Distance = distance,
                                    Duration = TimeSpan.FromSeconds(duration)
                                }
                            );

                            Log.Information(
                                "School: {School} => Distance: {Distance}, Duration: {Duration}",
                                schoolKey,
                                distance,
                                duration
                            );
                        }
                    }

                    return true;
                }
                finally
                {
                    _semaphore.Release();
                }
            },
            maxRetries: 20,
            delayMs: 500
        );

        Log.Information("Matrix routes obtained.");
        return results;
    }

    private static double Trimean(List<double> data)
    {
        if (data.Count == 0) return 0;
        Log.Information("Calculating trimean for {Count} data points.", data.Count);
        List<double> sorted = [.. data.OrderBy(x => x)];
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.5);
        double q3 = WeightedPercentile(sorted, 0.75);
        return (q1 + 2 * median + q3) / 4.0;

        static double WeightedPercentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0;
            double position = (sorted.Count - 1) * percentile;
            int left = (int)Math.Floor(position);
            int right = (int)Math.Ceiling(position);
            return left == right
                ? sorted[left]
                : sorted[left] + (sorted[right] - sorted[left]) * (position - left);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _tasks.Clear();
    }
}