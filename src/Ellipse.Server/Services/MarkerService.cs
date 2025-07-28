using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Helpers;
using Osrm.HttpApiClient;
using Serilog;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(
    GeoService geocoder,
    OsrmHttpApiClient client,
    OpenRouteClient openRouteClient,
    SupabaseStorageClient storageClient
) : IDisposable
{
    private const int MaxConcurrentBatches = 4;
    private const int MaxRetries = 20;
    private const int MatrixBatchSize = 25;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrentBatches);
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _currentTasks = [];
    private const string FolderName = "marker_cache";

    public async Task<MarkerResponse?> GetMarker(MarkerRequest request)
    {
        Log.Information("Called for point: {Point}", request.Point);

        string? cachedData = await storageClient.Get(request.Point, FolderName);
        if (!string.IsNullOrEmpty(cachedData) && !request.OverrideCache)
        {
            Log.Information("Cache hit for point: {Point}", request.Point);
            MarkerResponse deserialized = JsonSerializer.Deserialize<MarkerResponse>(
                StringHelper.DecompressString(cachedData)
            )!;
            Log.Information("Returning cached MarkerResponse");
            return deserialized;
        }

        Log.Information("Cache miss for point: {Point}", request.Point);

        MarkerResponse? markerResponse = await _currentTasks
            .GetOrAdd(request.Point, _ => ProcessMarkerRequest(request))
            .ConfigureAwait(false);

        if (markerResponse == null)
            Log.Warning("MarkerResponse is null for point: {Point}", request.Point);

        string serialized = JsonSerializer.Serialize(markerResponse);

        await storageClient.Set(request.Point, StringHelper.CompressString(serialized), FolderName);

        Log.Information("Cached new MarkerResponse for point: {Point}", request.Point);

        return markerResponse;
    }

    private async Task<MarkerResponse?> ProcessMarkerRequest(MarkerRequest request)
    {
        Log.Information("Processing MarkerRequest for point: {Point}", request.Point);

        if (request.Schools.Count == 0)
        {
            Log.Warning("No schools provided. Returning null.");
            return null;
        }

        string address = await geocoder
            .GetAddressCached(request.Point.Lon, request.Point.Lat)
            .ConfigureAwait(false);
        Log.Information("Address fetched: {Address}", address);

        var routes = await GetMatrixRoutes(request.Point, request.Schools).ConfigureAwait(false);
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
        Dictionary<string, Route> results = [];

        IEnumerable<SchoolData[]> batches = schools.Chunk(MatrixBatchSize);
        Log.Information("Schools chunked into {BatchCount} batches", MatrixBatchSize);

        await Task.WhenAll(
            batches.Select(async (batch, token) => await GetMatrixBatched(source, batch, results))
        );

        Log.Information("All batch tasks completed.");
        return results.ToDictionary();
    }

    private async Task GetMatrixBatched(
        GeoPoint2d source,
        SchoolData[] batch,
        Dictionary<string, Route> results
    ) =>
        _ = await Util.RetryIfInvalid(
            success => success,
            async (attempt) =>
            {
                Log.Information(
                    "Batch attempt {Retry} for batch with {Count} schools",
                    attempt,
                    batch.Length
                );
                await _semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    GeoPoint2d[] destinations = [.. batch.Select(s => s.LatLng)];
                    Log.Information(
                        "Calling GetMatrixBatch for {Count} destinations",
                        destinations.Length
                    );
                    TableResponse? response = await GetMatrixRoute(source, destinations)
                        .ConfigureAwait(false);

                    double[]? distances = response
                        ?.Distances[0]
                        .Select(x => (double)(x ?? 0))
                        .ToArray();

                    double[]? durations = response
                        ?.Durations[0]
                        .Select(x => (double)(x ?? 0))
                        .ToArray();

                    if (response == null || distances == null || durations == null)
                    {
                        OpenRouteMatrixResponse? openRouteResponse =
                            await GetMatrixRouteWithOpenRoute(source, destinations)
                                .ConfigureAwait(false);

                        if (
                            openRouteResponse == null
                            || openRouteResponse.Distances == null
                            || openRouteResponse.Durations == null
                        )
                        {
                            Log.Error("Invalid matrix response received.");
                            throw new InvalidOperationException("Invalid matrix response");
                        }

                        distances = openRouteResponse?.Distances[0];
                        durations = openRouteResponse?.Durations[0];
                    }

                    for (int i = 0; i < batch.Length; i++)
                    {
                        SchoolData school = batch[i];
                        double distance = distances[i];
                        double duration = durations[i];

                        results[school.Name] = new Route
                        {
                            Distance = MetersToMiles(distance),
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
                catch (Exception ex)
                {
                    Log.Error(ex, "Error occurred while processing batch: {Batch}", ex.Message);
                    return false;
                }
                finally
                {
                    _semaphore.Release();
                    Log.Information("Semaphore released.");
                }
            },
            false,
            MaxRetries,
            500
        );

    private async Task<TableResponse?> GetMatrixRoute(GeoPoint2d source, GeoPoint2d[] destinations)
    {
        Log.Information(
            "Called for source: {Source} with {Count} destinations.",
            source,
            destinations.Length
        );

        if (destinations.All(dest => dest == GeoPoint2d.Zero))
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
            .Destinations([.. Enumerable.Range(1, destinations.Length)])
            .Sources(0)
            .Annotations(TableAnnotations.DurationAndDistance)
            .Build();

        Log.Information("Request prepared. Calling MapboxClient.GetMatrixAsync...");
        TableResponse? response = await client.GetTableAsync(request);
        Log.Information("{Response}", response);

        if (response == null || response?.Durations == null || response?.Distances == null)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidOperationException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        return response;
    }

    private async Task<OpenRouteMatrixResponse?> GetMatrixRouteWithOpenRoute(
        GeoPoint2d source,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "Called for source: {Source} with {Count} destinations.",
            source,
            destinations.Length
        );

        if (destinations.Contains(GeoPoint2d.Zero))
            return null;

        OpenRouteMatrixRequest request = new OpenRouteMatrixRequest
        {
            Locations =
            [
                [source.Lon, source.Lat],
                .. destinations.Select(dest => new double[] { dest.Lon, dest.Lat }),
            ],
            Sources = [0],
            Destinations = [.. Enumerable.Range(1, destinations.Length)],
            Metrics = ["distance", "duration"],
            Units = "m",
            Profile = Profile.DrivingCar,
        };

        Log.Information("Request prepared. Calling OpenRouteClient.GetMatrixAsync...");
        OpenRouteMatrixResponse? response = await openRouteClient.GetMatrix(request);
        Log.Information("{Response}", response);

        if (response == null || response?.Durations == null || response?.Distances == null)
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

    private static double MetersToMiles(double meters) => meters * 0.000621371192;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _currentTasks.Clear();
        _semaphore.Dispose();
    }
}
