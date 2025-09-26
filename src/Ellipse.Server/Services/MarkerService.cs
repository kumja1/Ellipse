using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients;
using Serilog;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(GeocodingService geocodingService, SupabaseCache cache) : IDisposable
{
    private const string CacheFolderName = "markers";
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _tasks = [];
    private readonly SemaphoreSlim _semaphore = new(20, 20);

    public async Task<MarkerResponse?> GetMarker(MarkerRequest request)
    {
        Log.Information("Called for point: {Point}", request.Point);

        string? cachedData = await cache.Get(request.Point, CacheFolderName);
        if (!string.IsNullOrEmpty(cachedData) && !request.OverrideCache)
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
                .GetOrAdd(request.Point, _ => ProcessMarkerRequest(request))
                .ConfigureAwait(false);

            if (markerResponse == null)
            {
                Log.Warning("MarkerResponse is null for point: {Point}", request.Point);
                return null;
            }

            await cache.Set(
                request.Point,
                StringHelper.Compress(JsonSerializer.Serialize(markerResponse)),
                CacheFolderName
            );
            Log.Information("Cached new MarkerResponse for point: {Point}", request.Point);

            return markerResponse;
        }
        finally
        {
            _tasks.TryRemove(request.Point, out _);
        }
    }

    private async Task<MarkerResponse?> ProcessMarkerRequest(MarkerRequest request)
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
        var routes = await GetMatrixRoutes(request.Point, request.Schools).ConfigureAwait(false);

        Log.Information("Matrix routes obtained. Count: {Count}", routes.Count);
        if (routes.Count == 0)
        {
            Log.Warning("No routes found. Returning null.");
            return null;
        }

        List<double> distances = [.. routes.Values.Select(r => r.Distance)];
        List<double> durations = [.. routes.Values.Select(r => r.Duration)];

        double avgDistance = Trimean(distances);
        double avgDuration = Trimean(durations);

        routes["Average"] = new Route { Distance = avgDistance, Duration = avgDuration };
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
        Dictionary<string, Route> results = [];

        await GetMatrixRoute(source, schools, results);
        Log.Information("All tasks completed.");
        return results.ToDictionary();
    }

    private async Task<bool> GetMatrixRoute(
        GeoPoint2d source,
        List<SchoolData> schools,
        Dictionary<string, Route> results
    ) =>
        _ = await CallbackHelper.RetryIfInvalid(
            null,
            async (attempt) =>
            {
                Log.Information("Attempt {Retry} for {Count} schools", attempt, schools.Count);
                GeoPoint2d[] destinations = [.. schools.Select(s => s.LatLng)];

                await _semaphore.WaitAsync();
                try
                {
                    (double[] distances, double[] durations) =
                        await geocodingService.GetMatrixCached(source, destinations);

                    for (int i = 0; i < schools.Count; i++)
                    {
                        SchoolData school = schools[i];
                        double distance = distances![i];
                        double duration = durations![i];

                        results[school.Name] = new Route
                        {
                            Distance = distance / 1609,
                            Duration = duration,
                        };

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
