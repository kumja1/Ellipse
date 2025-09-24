using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Utils;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Clients.Mapping;
using Osrm.HttpApiClient;
using Serilog;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(
    GeocodingService geocoder,
    OsrmHttpApiClient client,
    OpenRouteClient openRouteClient,
    SupabaseStorageClient storageClient
) : IDisposable
{
    private const int MaxRetries = 20;
    private const string FolderName = "marker_cache";
    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse?>> _currentTasks = [];

    private readonly SemaphoreSlim _semaphore = new(5, 5);

    public async Task<MarkerResponse?> GetMarker(MarkerRequest request)
    {
        Log.Information("Called for point: {Point}", request.Point);

        string? cachedData = await storageClient.Get(request.Point, FolderName);
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

        MarkerResponse? markerResponse = await _currentTasks
            .GetOrAdd(request.Point, _ => ProcessMarkerRequest(request))
            .ConfigureAwait(false);

        _currentTasks.TryRemove(request.Point, out _);
        if (markerResponse == null)
        {
            Log.Warning("MarkerResponse is null for point: {Point}", request.Point);
            return null;
        }

        string serialized = StringHelper.Compress(JsonSerializer.Serialize(markerResponse));
        await storageClient.Set(request.Point, serialized, FolderName);
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
            IEnumerable<double> distances = routes.Values.Select(r => r.Distance);
            IEnumerable<double> durations = routes.Values.Select(r => r.Duration);
            double avgDistance = Trimean([.. distances]);
            double avgDuration = Trimean([.. durations]);
            routes["Average Distance"] = new Route
            {
                Distance = avgDistance,
                Duration = avgDuration,
            };

            routes["Total Distance"] = new Route
            {
                Distance = distances.Sum(),
                Duration = durations.Sum(),
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

        await GetMatrixRoute(source, schools, results);

        Log.Information("All batch tasks completed.");
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
                Log.Information(
                    "Batch attempt {Retry} for batch with {Count} schools",
                    attempt,
                    schools.Count
                );

                GeoPoint2d[] destinations = [.. schools.Select(s => s.LatLng)];
                await _semaphore.WaitAsync();

                try
                {
                    TableResponse? response = await GetMatrixRouteWithOSRM(source, destinations)
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

                        ArgumentNullException.ThrowIfNull(openRouteResponse);
                        ArgumentNullException.ThrowIfNull(openRouteResponse.Distances);
                        ArgumentNullException.ThrowIfNull(openRouteResponse.Durations);

                        distances = openRouteResponse.Distances[0];
                        durations = openRouteResponse.Durations[0];
                    }

                    for (int i = 0; i < schools.Count; i++)
                    {
                        SchoolData school = schools[i];
                        double distance = distances[i];
                        double duration = durations[i];

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
            maxRetries: MaxRetries,
            delayMs: 500
        );

    private async Task<TableResponse?> GetMatrixRouteWithOSRM(
        GeoPoint2d source,
        GeoPoint2d[] destinations
    )
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
        Log.Information("Matrix Response: {Response}", response);
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

        OpenRouteMatrixRequest request = new()
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _currentTasks.Clear();
    }
}
