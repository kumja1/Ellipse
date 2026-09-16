using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Directions;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using Ellipse.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

namespace Ellipse.Services;

public class MarkerService(GeocodingService geocodingService, IDistributedCache cache)
    : IDisposable
{
    private readonly ConcurrentDictionary<LngLat, Task<MarkerResponse?>> _tasks = [];
    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<MarkerResponse[]> GetMarkers(BatchMarkerRequest request, bool overwriteCache)
    {
        string cacheKey = CacheHelper.CreateCacheKey(request.Points, request.Schools);
        if (!overwriteCache)
        {
            string? cachedData = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Log.Information("Cache hit for batch request: {RequestId}", cacheKey);
                MarkerResponse[] deserialized = JsonSerializer.Deserialize<MarkerResponse[]>(cachedData)!;

                Log.Information("Returning cached MarkerResponse");
                return deserialized;
            }
        }

        MarkerResponse[] results = await GetMarkersInternal(request).ConfigureAwait(false);
        if (results.Length != 0)
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results));

        return results;
    }

    private async Task<MarkerResponse[]> GetMarkersInternal(BatchMarkerRequest request)
    {
        Dictionary<LngLat, MarkerResponse?> resultsMap = new(request.Points.Length);
        string[] addresses = await Task.WhenAll(
            request.Points.Select(p => geocodingService.GetAddressCached(p.Lng, p.Lat))
        );

        Dictionary<string, SchoolRoute>[] allRoutes =
            await GetMatrixRoute(request.Points, request.Schools).ConfigureAwait(false);

        for (int i = 0; i < request.Points.Length; i++)
        {
            LngLat point = request.Points[i];
            Dictionary<string, SchoolRoute> routes = allRoutes[i];

            MarkerResponse? response = CreateResponse(routes, request.Schools, point, addresses[i]);
            resultsMap[point] = response;
        }

        return [.. resultsMap.Values];
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
                MarkerResponse deserialized = JsonSerializer.Deserialize<MarkerResponse>(cachedData)!;
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
                JsonSerializer.Serialize(markerResponse)
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
            .GetAddressCached(request.Point.Lng, request.Point.Lat)
            .ConfigureAwait(false);

        Log.Information("Address fetched: {Address}", address);
        Dictionary<string, SchoolRoute>[] allRoutes =
            await GetMatrixRoute([request.Point], request.Schools).ConfigureAwait(false);

        Dictionary<string, SchoolRoute> routes = allRoutes[0];
        Log.Information("Matrix routes obtained. Count: {Count}", routes.Count);

        MarkerResponse? response = CreateResponse(routes, request.Schools, request.Point, address);
        return response;
    }

    private MarkerResponse? CreateResponse(Dictionary<string, SchoolRoute> routes, SchoolData[] schools, LngLat point, string address)
    {
        if (routes.Count == 0)
        {
            Log.Warning("No routes found. Returning null.");
            return null;
        }

        var divisionMetrices = schools
            .Where(s => s.LngLat != LngLat.Zero)
            .GroupBy(s => s.Division)
            .Select(g =>
            {
                double[] durations = [.. g.Select(s => routes[$"{s.Name} ({s.Division})"].Duration.TotalSeconds)];
                double[] distances = [.. g.Select(s => routes[$"{s.Name} ({s.Division})"].Distance)];
                return new
                {
                    TrimeanDuration = Trimean(durations),
                    TrimeanDistance = Trimean(distances)
                };
            })
            .Where(x => !double.IsNaN(x.TrimeanDuration) && !double.IsNaN(x.TrimeanDistance))
            .ToList();

        if (divisionMetrices.Count == 0)
        {
            Log.Warning("No valid schools with coordinates for point: {Point}", point);
            return null;
        }

        double totalDistance = routes.Sum(kvp => kvp.Value.Distance);
        double totalDuration = routes.Sum(kvp => kvp.Value.Duration.TotalSeconds);

        double averageDuration = divisionMetrices.Average(x => x.TrimeanDuration);
        double averageDistance = divisionMetrices.Average(x => x.TrimeanDistance);


        routes["Average"] = new SchoolRoute
        { Distance = averageDistance, Duration = TimeSpan.FromSeconds(averageDuration) };


        return new MarkerResponse(address, point, totalDistance, TimeSpan.FromSeconds(totalDuration), routes);

    }


    private async Task<Dictionary<string, SchoolRoute>[]> GetMatrixRoute(
        LngLat[] sources,
        SchoolData[] schools
    )
    {
        Dictionary<string, SchoolRoute>[] results = [.. Enumerable.Range(0, sources.Length).Select(_ => new Dictionary<string, SchoolRoute>(schools.Length))];

        await Retry.Default(
            async attempt =>
            {
                Log.Information("Attempt {Retry} for {Count} schools", attempt, schools.Length);
                await _semaphore.WaitAsync();
                try
                {
                    (float?[][]? distances, float?[][]? durations) =
                        await geocodingService.GetMatrixCached(
                            sources,
                            [.. schools.Select(s => s.LngLat)]
                        );

                    if (distances?.Length == 0 || durations?.Length == 0)
                    {
                        Log.Warning("Received empty matrix from GeocodingService. Retrying...");
                        return false;
                    }
                    for (int i = 0; i < sources.Length; i++)
                    {
                        Dictionary<string, SchoolRoute> currentDict = results[i];
                        for (int j = 0; j < schools.Length; j++)
                        {
                            SchoolData school = schools[j];
                            string schoolKey = $"{school.Name} ({school.Division})";

                            float? distance = distances[i][j];
                            float? duration = durations[i][j];

                            if (distance == null || duration == null)
                            {
                                Log.Warning("Distance or duration is null for school: {School}", schoolKey);
                                continue;
                            }

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
                                    Distance = distance.Value,
                                    Duration = TimeSpan.FromSeconds((double)duration)
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

    private static double Trimean(double[] data)
    {
        if (data.Length == 0)
            return 0;

        Array.Sort(data);
        double q1 = WeightedPercentile(data, 0.25);
        double median = WeightedPercentile(data, 0.5);
        double q3 = WeightedPercentile(data, 0.75);
        return (q1 + 2 * median + q3) / 4.0;

        static double WeightedPercentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0) return 0;
            double position = (sorted.Length - 1) * percentile;
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
        _semaphore.Dispose();
    }
}