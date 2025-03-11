using Ellipse.Common.Models;
using Ellipse.Common.Enums;
using Ellipse.Common.Enums.Directions;
using DirectionsRequest = Ellipse.Common.Models.Directions.DirectionsRequest;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Ellipse.Common.Models.Markers;
using Route = Ellipse.Common.Models.Directions.Route;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Ellipse.Server.Services;

public class MarkerService
{
    private readonly GeoService _geocoder;
    private readonly MapboxClient _mapboxService;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private readonly ConcurrentDictionary<GeoPoint2d, Task<MarkerResponse>> _currentTasks = [];

    public MarkerService(GeoService geocoder, MapboxClient mapboxService)
    {
        _geocoder = geocoder;
        _mapboxService = mapboxService;
    }

    public async Task<MarkerResponse> GetMarkerByLocation(MarkerRequest request)
    {

        if (_cache.TryGetValue(request.Point, out string? cachedData))
            return JsonSerializer.Deserialize<MarkerResponse>(cachedData)!;

        return await _currentTasks.GetOrAdd(request.Point, _ => Task.Run(async () =>
         {
             var schools = request.Schools;
             var loc = request.Point;
             if (schools.Count == 0)
                 return null;

             var latLngs = schools.Select(s => s.LatLng).ToList();
             var distances = await GetDistances(schools, latLngs, loc.Lat, loc.Lon).ConfigureAwait(false);
             var addressTask = _geocoder.GetAddressCached(loc.Lat, loc.Lon).ConfigureAwait(false);

             if (distances.Count == 0)
                 return null;

             var triemeanDistance = Trimean(distances.Values.Select(d => d.Distance).ToList());
             var triemeanDuration = TimeSpan.FromSeconds(
                 Trimean(distances.Values.Select(d => d.Duration).ToList())
             );

             return new MarkerResponse(await addressTask, distances);
         })!).ConfigureAwait(false);

    }

    private async Task<Dictionary<string, Route>> GetDistances(
        List<SchoolData> schools, List<GeoPoint2d> destinations, double sourceX, double sourceY)
    {
        try
        {
            var sourceGeoPoint = new GeoPoint2d(sourceX, sourceY);
            var distances = new ConcurrentDictionary<string, Route>();

            await Parallel.ForEachAsync(Enumerable.Range(0, destinations.Count), async (index, _) =>
            {
                var destination = destinations[index];

                var request = new DirectionsRequest
                {
                    Annotations = [DirectionsAnnotationType.Distance, DirectionsAnnotationType.Duration],
                    Profile = RoutingProfile.Driving,
                    Overview = OverviewType.Full,
                    Alternatives = true,
                    AccessToken = "your_mapbox_access_token",
                    Waypoints = [destination, sourceGeoPoint]
                };

                var response = await _mapboxService.GetDirectionsAsync(request).ConfigureAwait(false);
                if (response is { Routes.Count: > 0 })
                {
                    var route = response.Routes[0];
                    route.Distance = MetersToMiles(route.Distance);
                    distances[schools[index].Name] = route;
                }
            }).ConfigureAwait(false);

            return distances.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetDistances: {ex.Message}");
            return new Dictionary<string, Route>();
        }
    }

    static double Trimean(List<double> data)
    {
        var sorted = data.OrderBy(x => x).ToList();
        double q1 = WeightedPercentile(sorted, 0.25);
        double median = WeightedPercentile(sorted, 0.50);
        double q3 = WeightedPercentile(sorted, 0.75);
        return (q1 + 2 * median + q3) / 4.0;
    }

    static double WeightedPercentile(List<double> sorted, double percentile)
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
}
