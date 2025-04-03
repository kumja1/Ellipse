using Ellipse.Common.Models;
using Ellipse.Common.Enums;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Ellipse.Common.Models.Markers;
using Route = Ellipse.Common.Models.Directions.Route;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Ellipse.Common.Models.Matrix;
using Ellipse.Common.Enums.Matrix;

namespace Ellipse.Server.Services;

public class MarkerService(GeoService geocoder, MapboxClient mapboxService) : IDisposable
{
    private const int MAX_CONCURRENT_BATCHES = 2;
    private const int MAX_RETRIES = 6;
    private const int MATRIX_BATCH_SIZE = 23;

    private readonly GeoService _geocoder = geocoder;
    private readonly MapboxClient _mapboxService = mapboxService;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly SemaphoreSlim _semaphore = new(MAX_CONCURRENT_BATCHES);
    private readonly ConcurrentDictionary<GeoPoint2d, ValueTask<MarkerResponse?>> _currentTasks = new();

    private readonly string? _mapboxAccessToken = Environment.GetEnvironmentVariable("MAPBOX_API_KEY");

    public async ValueTask<MarkerResponse?> GetMarkerByLocation(MarkerRequest request)
    {
        if (_cache.TryGetValue(request.Point, out string? cachedData))
            return JsonSerializer.Deserialize<MarkerResponse>(cachedData)!;

        var markerResponse = await _currentTasks.GetOrAdd(
            request.Point,
            _ => ProcessMarkerRequestAsync(request)
        ).ConfigureAwait(false);

        if (markerResponse != null)
        {
            _cache.Set(request.Point, JsonSerializer.Serialize(markerResponse),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(10) });
        }

        return markerResponse;
    }

    private async ValueTask<MarkerResponse?> ProcessMarkerRequestAsync(MarkerRequest request)
    {
        if (request.Schools.Count == 0) return null;

        var addressTask = _geocoder.GetAddressCached(request.Point.Lat, request.Point.Lon);
        var routesTask = GetMatrixRoutes(request.Point, request.Schools);

        var address = await addressTask.ConfigureAwait(false);
        var routes = await routesTask.ConfigureAwait(false);

        if (routes.Count > 0)
        {
            routes["Average Distance"] = new Route
            {
                Distance = Trimean(routes.Values.Select(r => r.Distance).ToList()),
                Duration = Trimean(routes.Values.Select(r => r.Duration).ToList())
            };
        }

        return routes.Count == 0 ? null : new MarkerResponse(address, routes);
    }

    private async Task<Dictionary<string, Route>> GetMatrixRoutes(
        GeoPoint2d source, List<SchoolData> schools)
    {
        var results = new ConcurrentDictionary<string, Route>();
        var batches = schools.Chunk(MATRIX_BATCH_SIZE);

        var batchTasks = batches.Select(async batch =>
        {
            for (int retry = 0; retry <= MAX_RETRIES; retry++)
            {
                await _semaphore.WaitAsync(1000);
                try
                {
                    var response = await GetMatrixBatch(source, batch.Select(s => s.LatLng).ToList());

                    for (int i = 0; i < batch.Length; i++)
                    {
                        var school = batch[i];
                        results[school.Name] = new Route
                        {
                            Distance = MetersToMiles(response.Distances[0][i]),
                            Duration = response.Durations[0][i]
                        };
                    }
                    return;
                }
                catch (Exception ex) when (retry < MAX_RETRIES)
                {
                    Console.Error.WriteLine($"Matrix API call failed on attempt {retry + 1}: {ex.Message}");
                    await Task.Delay(500 * (int)Math.Pow(2, retry));
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        });

        await Task.WhenAll(batchTasks);
        return new Dictionary<string, Route>(results);
    }

    private async Task<MatrixResponse> GetMatrixBatch(
        GeoPoint2d source,
        List<GeoPoint2d> destinations)
    {
        if (string.IsNullOrWhiteSpace(_mapboxAccessToken))
            throw new InvalidOperationException("MAPBOX_API_KEY environment variable is missing or empty.");

        var request = new MatrixRequest
        {
            Sources = [source],
            Destinations = destinations,
            Profile = RoutingProfile.Driving,
            Annotations = [MatrixAnnotationType.Distance, MatrixAnnotationType.Duration],
            AccessToken = _mapboxAccessToken
        };

        var response = await _mapboxService.GetMatrixAsync(request);

        if (response?.Durations == null || response.Distances == null)
            throw new InvalidOperationException("Invalid matrix response");

        return response;
    }

    private static double Trimean(List<double> data)
    {
        var sorted = data.OrderBy(x => x).ToList();
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.50);
        double q3 = WeightedPercentile(sorted, 0.75);
        return (q1 + 2 * median + q3) / 4.0;
    }

    private static double WeightedPercentile(List<double> sorted, double percentile)
    {
        double rank = percentile * (sorted.Count - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        double weight = rank - lowerIndex;
        return sorted[lowerIndex] * (1 - weight) + sorted[upperIndex] * weight;
    }

    private static double MetersToMiles(double meters) => meters / 1609.34;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _semaphore.Dispose();
    }
}
