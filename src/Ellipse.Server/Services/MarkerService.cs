using System.Collections.Concurrent;
using System.Text.Json;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Markers;
using Microsoft.Extensions.Caching.Memory;
using Osrm.HttpApiClient;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Route = Ellipse.Common.Models.Directions.Route;

namespace Ellipse.Server.Services;

public class MarkerService(GeoService geocoder, OsrmHttpApiClient client) : IDisposable
{
    private const int MAX_CONCURRENT_BATCHES = 4;
    private const int MAX_RETRIES = 5;
    private const int MATRIX_BATCH_SIZE = 25;
    private readonly GeoService _geocoder = geocoder;
    private readonly OsrmHttpApiClient _osrmClient = client;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly SemaphoreSlim _semaphore = new(MAX_CONCURRENT_BATCHES);
    private readonly ConcurrentDictionary<GeoPoint2d, ValueTask<MarkerResponse?>> _currentTasks =
        new();

    public async ValueTask<MarkerResponse?> GetMarkerByLocation(MarkerRequest request)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] Called for point: {request.Point}"
        );

        if (_cache.TryGetValue(request.Point, out string? cachedData) && !request.OverrideCache)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] Cache hit for point: {request.Point}"
            );
            var deserialized = JsonSerializer.Deserialize<MarkerResponse>(cachedData)!;
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] Returning cached MarkerResponse"
            );
            return deserialized;
        }
        else
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] Cache miss for point: {request.Point}"
            );
        }

        var markerResponse = await _currentTasks
            .GetOrAdd(request.Point, _ => ProcessMarkerRequestAsync(request))
            .ConfigureAwait(false);

        if (markerResponse != null)
        {
            string serialized = JsonSerializer.Serialize(markerResponse);
            _cache.Set(
                request.Point,
                serialized,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(10),
                }
            );
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] Cached new MarkerResponse for point: {request.Point}"
            );
        }
        else
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMarkerByLocation] MarkerResponse is null for point: {request.Point}"
            );
        }

        return markerResponse;
    }

    private async ValueTask<MarkerResponse?> ProcessMarkerRequestAsync(MarkerRequest request)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] Processing MarkerRequest for point: {request.Point}"
        );
        if (request.Schools.Count == 0)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] No schools provided. Returning null."
            );
            return null;
        }

        var addressTask = _geocoder.GetAddressCached(request.Point.Lon, request.Point.Lat);
        var routesTask = GetMatrixRoutes(request.Point, request.Schools);

        string address = await addressTask.ConfigureAwait(false);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] Address fetched: {address}"
        );

        var routes = await routesTask.ConfigureAwait(false);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] Matrix routes obtained. Count: {routes.Count}"
        );

        if (routes.Count > 0)
        {
            double avgDistance = Trimean(routes.Values.Select(r => r.Distance).ToList());
            double avgDuration = Trimean(routes.Values.Select(r => r.Duration).ToList());
            routes["Average Distance"] = new Route
            {
                Distance = avgDistance,
                Duration = avgDuration,
            };
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] Calculated average route: Distance={avgDistance}, Duration={avgDuration}"
            );
        }
        else
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [ProcessMarkerRequestAsync] No routes found."
            );
        }

        return routes.Count == 0 ? null : new MarkerResponse(address, routes);
    }

    private async Task<Dictionary<string, Route>> GetMatrixRoutes(
        GeoPoint2d source,
        List<SchoolData> schools
    )
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Called for source: {source} with {schools.Count} schools"
        );
        var results = new ConcurrentDictionary<string, Route>();
        var batches = schools.Chunk(MATRIX_BATCH_SIZE);
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Schools chunked into {batches.Count()} batches"
        );

        await Parallel.ForEachAsync(
            batches,
            async (batch, token) => await GetMatrixBatch(source, batch, results, token)
        );

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] All batch tasks completed."
        );
        return results.ToDictionary();
    }

    private async Task GetMatrixBatch(
        GeoPoint2d source,
        SchoolData[] batch,
        ConcurrentDictionary<string, Route> results,
        CancellationToken token
    )
    {
        for (int retry = 0; retry <= MAX_RETRIES; retry++)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Batch attempt {retry} for batch with {batch.Length} schools"
            );
            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<GeoPoint2d> destinationList = [.. batch.Select(s => s.LatLng)];
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Calling GetMatrixBatch for {destinationList.Count} destinations"
                );
                var response = await GetMatrixRoute(source, destinationList).ConfigureAwait(false);
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Received matrix response"
                );

                if (
                    response.Distances.Length == 0
                    || response.Durations.Length == 0
                    || response.Distances[0].Length < batch.Length
                )
                {
                    throw new InvalidOperationException("Incomplete matrix response");
                }

                for (int i = 0; i < batch.Length; i++)
                {
                    var school = batch[i];

                    var distance = response.Distances[0][i];
                    var duration = response.Durations[0][i];
                    if (!distance.HasValue || !duration.HasValue)
                    {
                        Console.WriteLine(
                            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Failed to caculate route for {school.Name}"
                        );
                        continue;
                    }

                    results[school.Name] = new Route
                    {
                        Distance = MetersToMiles(distance.Value),
                        Duration = duration.Value,
                    };
                    Console.WriteLine(
                        $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] School: {school.Name} => Distance: {distance}, Duration: {duration}"
                    );
                }
                break;
            }
            catch (Exception ex) when (retry < MAX_RETRIES)
            {
                Console.Error.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Matrix API call failed on attempt {retry + 1}: {ex.Message}"
                );
                int delayMs = 500 * (int)Math.Pow(2, retry);
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Waiting {delayMs}ms before retrying."
                );
                await Task.Delay(delayMs, token).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixRoutes] Semaphore released."
                );
            }
        }
    }

    private async Task<TableResponse> GetMatrixRoute(
        GeoPoint2d source,
        List<GeoPoint2d> destinations
    )
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixBatch] Called for source: {source} with {destinations.Count} destinations."
        );
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

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixBatch] Request prepared. Calling MapboxClient.GetMatrixAsync..."
        );
        var response = await _osrmClient.GetTableAsync(request);
        if (response?.Durations == null || response.Distances == null)
        {
            Console.Error.WriteLine(
                $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixBatch] Invalid matrix response received."
            );
            throw new InvalidOperationException("Invalid matrix response");
        }

        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [GetMatrixBatch] Matrix response successfully received."
        );
        return response;
    }

    private static double Trimean(List<double> data)
    {
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [Trimean] Calculating trimean for {data.Count} data points."
        );
        var sorted = data.OrderBy(x => x).ToList();
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.50);
        double q3 = WeightedPercentile(sorted, 0.75);
        double trimean = (q1 + 2 * median + q3) / 4.0;
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [Trimean] q1: {q1}, Median: {median}, q3: {q3}, Trimean: {trimean}"
        );
        return trimean;
    }

    private static double WeightedPercentile(List<double> sorted, double percentile)
    {
        double rank = percentile * (sorted.Count - 1);
        int lowerIndex = (int)Math.Floor(rank);
        int upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        double weight = rank - lowerIndex;
        double result = sorted[lowerIndex] * (1 - weight) + sorted[upperIndex] * weight;
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [WeightedPercentile] Percentile: {percentile}, Rank: {rank}, Result: {result}"
        );
        return result;
    }

    private static double MetersToMiles(float meters)
    {
        double miles = meters / 1609.34;
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss.fff}] [MetersToMiles] Converted {meters} meters to {miles} miles."
        );
        return miles;
    }

    public void Dispose()
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Dispose] Disposing MarkerService.");
        GC.SuppressFinalize(this);
        _semaphore.Dispose();
    }
}
