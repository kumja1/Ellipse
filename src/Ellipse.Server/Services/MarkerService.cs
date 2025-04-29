using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Server.Utils.Helpers;
using Ellipse.Server.Utils.Objects;
using Osrm.HttpApiClient;
using Serilog;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(
    GeoService geocoder,
    OsrmHttpApiClient client,
    SupabaseStorageClient storageClient
) : IDisposable
{
    private const int MaxConcurrentBatches = 4;
    private const int MaxRetries = 5;
    private const int MatrixBatchSize = 25;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentBatches);
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _currentTasks = new();

    public async Task<MarkerResponse?> GetMarkerByLocation(MarkerRequest request)
    {
        Log.Information("Called for point: {Point}", request.Point);

        string? cachedData = await storageClient.Get(request.Point);
        if (!string.IsNullOrEmpty(cachedData) && !request.OverrideCache)
        {
            Log.Information("Cache hit for point: {Point}", request.Point);
            var deserialized = JsonSerializer.Deserialize<MarkerResponse>(
                StringCompressor.DecompressString(cachedData)
            )!;
            Log.Information("Returning cached MarkerResponse");
            return deserialized;
        }

        Log.Information("Cache miss for point: {Point}", request.Point);

        var markerResponse = await _currentTasks
            .GetOrAdd(request.Point, _ => ProcessMarkerRequestAsync(request))
            .ConfigureAwait(false);

        if (markerResponse == null)
            Log.Warning("MarkerResponse is null for point: {Point}", request.Point);

        string serialized = JsonSerializer.Serialize(markerResponse);

        await storageClient.Set(request.Point, StringCompressor.CompressString(serialized));

        Log.Information("Cached new MarkerResponse for point: {Point}", request.Point);

        return markerResponse;
    }

    private async Task<MarkerResponse?> ProcessMarkerRequestAsync(MarkerRequest request)
    {
        Log.Information("Processing MarkerRequest for point: {Point}", request.Point);

        if (request.Schools.Count == 0)
        {
            Log.Warning("No schools provided. Returning null.");
            return null;
        }

        var addressTask = geocoder.GetAddressCached(request.Point.Lon, request.Point.Lat);
        var routesTask = GetMatrixRoutes(request.Point, request.Schools);

        string address = await addressTask.ConfigureAwait(false);
        Log.Information("Address fetched: {Address}", address);

        var routes = await routesTask.ConfigureAwait(false);
        Log.Information("Matrix routes obtained. Count: {Count}", routes.Count);

        if (routes.Count > 0)
        {
            double avgDistance = Trimean(routes.Values.Select(r => r.Distance).ToList());
            double avgDuration = Trimean(routes.Values.Select(r => r.Duration).ToList());
            routes["Average Distance"] = new Route
            {
                Distance = avgDistance,
                Duration = avgDuration,
            };
            Log.Information(
                "Calculated average route: Distance={Distance}, Duration={Duration}",
                avgDistance,
                avgDuration
            );
        }
        else
        {
            Log.Warning("No routes found.");
        }

        return routes.Count == 0 ? null : new MarkerResponse(address, routes);
    }

    private async Task<Dictionary<string, Route>> GetMatrixRoutes(
        GeoPoint2d source,
        List<SchoolData> schools
    )
    {
        Log.Information("Called for source: {Source} with {Count} schools", source, schools.Count);
        var results = new ConcurrentDictionary<string, Route>();
        List<SchoolData[]> batches = [.. schools.Chunk(MatrixBatchSize)];
        Log.Information("Schools chunked into {BatchCount} batches", batches.Count);

        await Task.WhenAll(
            batches.Select(async (batch, token) => await GetMatrixBatch(source, batch, results))
        );

        Log.Information("All batch tasks completed.");
        return results.ToDictionary();
    }

    private async Task GetMatrixBatch(
        GeoPoint2d source,
        SchoolData[] batch,
        ConcurrentDictionary<string, Route> results
    )
    {
        for (int retry = 0; retry <= MaxRetries; retry++)
        {
            Log.Information(
                "Batch attempt {Retry} for batch with {Count} schools",
                retry,
                batch.Length
            );
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                List<GeoPoint2d> destinationList = [.. batch.Select(s => s.LatLng)];
                Log.Information(
                    "Calling GetMatrixBatch for {Count} destinations",
                    destinationList.Count
                );
                var response = await GetMatrixRoute(source, destinationList).ConfigureAwait(false);
                Log.Information("Received matrix response");

                if (
                    response == null
                    || response.Distances.Length == 0
                    || response.Durations.Length == 0
                    || response.Distances[0].Length < batch.Length
                )
                    throw new InvalidOperationException("Incomplete matrix response");

                for (int i = 0; i < batch.Length; i++)
                {
                    var school = batch[i];
                    var distance = response.Distances[0][i];
                    var duration = response.Durations[0][i];
                    if (!distance.HasValue || !duration.HasValue)
                    {
                        Log.Warning("Failed to calculate route for {School}", school.Name);
                        continue;
                    }

                    results[school.Name] = new Route
                    {
                        Distance = MetersToMiles(distance.Value),
                        Duration = duration.Value,
                    };
                    Log.Information(
                        "School: {School} => Distance: {Distance}, Duration: {Duration}",
                        school.Name,
                        distance,
                        duration
                    );
                }

                break;
            }
            catch (Exception ex) when (retry < MaxRetries)
            {
                Log.Warning(ex, "Matrix API call failed on attempt {Attempt}", retry + 1);
                int delayMs = 500 * (int)Math.Pow(2, retry);

                Log.Information("Waiting {Delay}ms before retrying.", delayMs);
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
                Log.Information("Semaphore released.");
            }
        }
    }

    private async Task<TableResponse?> GetMatrixRoute(
        GeoPoint2d source,
        List<GeoPoint2d> destinations
    )
    {
        Log.Information(
            "Called for source: {Source} with {Count} destinations.",
            source,
            destinations.Count
        );
        Log.Information("Destinations: {Destinations}", string.Join("\n", destinations));

        if (destinations.Contains(GeoPoint2d.Zero))
            return null;

        var request = OsrmServices
            .Table(
                PredefinedProfiles.Car,
                GeographicalCoordinates.Create(
                    [
                        Coordinate.Create(source.Lon, source.Lat),
                        .. destinations.Select(dest => Coordinate.Create(dest.Lon, dest.Lat)),
                    ]
                )
            )
            .Destinations([.. Enumerable.Range(1, destinations.Count)])
            .Sources(0)
            .Build();

        Log.Information("Request prepared. Calling MapboxClient.GetMatrixAsync...");
        var response = await client.GetTableAsync(request);
        Log.Information("{Response}", response);

        if (response?.Durations == null || response?.Distances == null)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidOperationException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        return response;
    }

    private static double Trimean(List<double> data)
    {
        Log.Information("Calculating trimean for {Count} data points.", data.Count);
        List<double> sorted = [.. data.OrderBy(x => x)];
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.5);
        double q3 = WeightedPercentile(sorted, 0.75);
        return (q1 + 2 * median + q3) / 4.0;
    }

    private static double WeightedPercentile(List<double> sorted, double percentile)
    {
        double position = (sorted.Count - 1) * percentile;
        int left = (int)Math.Floor(position);
        int right = (int)Math.Ceiling(position);
        return left == right
            ? sorted[left]
            : sorted[left] + (sorted[right] - sorted[left]) * (position - left);
    }

    private static double MetersToMiles(double meters) => meters * 0.000621371;

    public void Dispose() => _semaphore.Dispose();
}
