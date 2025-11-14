using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(GeocodingService geocodingService, IDistributedCache cache) : IDisposable
{
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _tasks = [];
    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<MarkerResponse?> GetMarker(MarkerRequest request, bool overwriteCache)
    {
        Log.Information("Called for point: {Point}", request.Point);

        string? cachedData = await cache.GetStringAsync($"marker_{request.Point}");
        if (!string.IsNullOrEmpty(cachedData) && !overwriteCache)
        {
            Log.Information("Cache hit for point: {Point}", request.Point);
            MarkerResponse deserialized = JsonSerializer.Deserialize<MarkerResponse>(
                StringHelper.Decompress(cachedData)
            )!;
            Log.Information("Returning cached MarkerResponse");
            return deserialized;
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
                $"marker_{request.Point}",
                StringHelper.Compress(JsonSerializer.Serialize(markerResponse))
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
        if (request.Schools.Count == 0)
        {
            Log.Warning("No schools provided. Returning null.");
            return null;
        }

        string address = await geocodingService
            .GetAddressCached(request.Point.Lon, request.Point.Lat)
            .ConfigureAwait(false);

        Log.Information("Address fetched: {Address}", address);
        Dictionary<string, Route> routes = await GetMatrixRoutes(request.Point, request.Schools).ConfigureAwait(false);

        Log.Information("Matrix routes obtained. Count: {Count}", routes.Count);
        if (routes.Count == 0)
        {
            Log.Warning("No routes found. Returning null.");
            return null;
        }

        List<double> distances = [.. routes.Values.Select(r => r.Distance)];
        double avgDistance = Trimean(distances);
        double avgDuration = Trimean([.. routes.Values.Select(r => r.Duration.TotalSeconds)]);

        routes["Average"] = new Route { Distance = avgDistance, Duration = TimeSpan.FromSeconds(avgDuration) };
        Log.Information(
            "Calculated average route: Distance={Distance}, Duration={Duration}",
            avgDistance,
            avgDuration
        );

        return routes.Count == 0 ? null : new MarkerResponse(address, distances.Sum(), routes);
    }

    private async Task<Dictionary<string, Route>> GetMatrixRoutes(
        GeoPoint2d source,
        List<SchoolData> schools
    )
    {
        Log.Information("Called for source: {Source} with {Count} schools", source, schools.Count);
        return await GetMatrixRoute(source, schools);
    }

    private async Task<Dictionary<string, Route>> GetMatrixRoute(
        GeoPoint2d source,
        List<SchoolData> schools
    )
    {
        Dictionary<string, Route> results = new(schools.Count);
        await CallbackHelper.RetryIfInvalid(
             null,
             async (attempt) =>
             {
                 Log.Information("Attempt {Retry} for {Count} schools", attempt, schools.Count);
                 await _semaphore.WaitAsync();
                 try
                 {
                     (double[] distances, double[] durations) =
                         await geocodingService.GetMatrixCached(
                             source,
                             [.. schools.Select(s => s.LatLng)]
                         );

                     for (int i = 0; i < schools.Count; i++)
                     {
                         SchoolData school = schools[i];
                         double distance = distances![i];
                         double duration = durations![i];

                         if (
                             !results.TryAdd(
                                 school.Name,
                                 new Route { Distance = distance, Duration = TimeSpan.FromSeconds(duration) }
                             )
                         )
                         {
                             Log.Information(
                                 "Route already exist for school: {School} => Distance: {Distance}, Duration: {Duration}",
                                 school.Name,
                                 distance,
                                 duration
                             );
                             continue;
                         }

                         Log.Information(
                             "School: {School} => Distance: {Distance}, Duration: {Duration}",
                             school.Name,
                             distance,
                             duration
                         );
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
        Log.Information("Calculating trimean for {Count} data points.", data.Count);
        List<double> sorted = [.. data.OrderBy(x => x)];
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.5);
        double q3 = WeightedPercentile(sorted, 0.75);
        return (q1 + 2 * median + q3) / 4.0;

        static double WeightedPercentile(List<double> sorted, double percentile)
        {
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
