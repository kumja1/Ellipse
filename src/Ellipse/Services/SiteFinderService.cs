using OpenLayers.Blazor;
using Ellipse.Common.Models;
using GeoPoint2d = Ellipse.Common.Models.GeoPoint2d;
using Ellipse.Common.Enums;
using Ellipse.Common.Enums.Directions;
using DirectionsRequest = Ellipse.Common.Models.Directions.DirectionsRequest;


namespace Ellipse.Services;

public class SiteFinderService(GeoService geocoder, MapboxClient mapboxService, SchoolLocatorService schoolLocator)
{
    private readonly GeoService _geocoder = geocoder;
    private readonly SchoolLocatorService _schoolLocator = schoolLocator;
    private readonly MapboxClient _mapboxService = mapboxService;
    private const double STEP_SIZE = 0.1;

    public async IAsyncEnumerable<Marker> GetMarkers()
    {
        var schools = await _schoolLocator.GetSchools();
        if (schools.Count == 0)
            yield break;

        var latLngs = schools.Select(school => school.LatLng).ToList();
        var boundingBox = new BoundingBox(latLngs);

        foreach (var batch in GenerateGrid(boundingBox).Chunk(5))
        {
            var tasks = batch.Select(async p => await GetMarker(schools, latLngs, p));
            foreach (var result in await Task.WhenAll(tasks))
            {
                if (result != null)
                    yield return result;
            }
        }
    }

    async Task<Marker?> GetMarker(List<SchoolData> schools, List<GeoPoint2d> latLngs, (double x, double y) point)
    {
        var (x, y) = point;
        var distances = await GetDistances(schools, latLngs, x, y);
        if (distances.Count == 0)
            return null;

        var triemeanDistance = Trimean([.. distances.Values.Select(d => d.Distance)]);
        var triemeanDuration = TimeSpan.FromSeconds(Trimean([.. distances.Values.Select(d => double.Parse(d.Duration.Split("|")[0].Trim()))]));

        distances["Average Distance"] = (triemeanDistance, FormatTimeSpan(triemeanDuration));
        return new Marker(MarkerType.MarkerPin, new Coordinate(y, x), null, PinColor.Blue)
        {
            Properties =
                {
                    ["Name"] = await _geocoder.GetAddressCached(x, y),
                    ["Distances"] = distances,
                },
            Scale = 0.1,
            Popup = true,
        };
    }

    static double Trimean(List<double> data)
    {
        ArgumentNullException.ThrowIfNull(data);
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

    private static IEnumerable<(double x, double y)> GenerateGrid(BoundingBox boundingBox)
    {
        for (var x = boundingBox.MinLat; x <= boundingBox.MaxLat; x += STEP_SIZE)
            for (var y = boundingBox.MinLng; y <= boundingBox.MaxLng; y += STEP_SIZE)
                yield return (x, y);
    }

    private async Task<Dictionary<string, (double Distance, string Duration)>> GetDistances(List<SchoolData> schools, List<GeoPoint2d> destinations, double sourceX, double sourceY)
    {
        try
        {
            var distances = new Dictionary<string, (double Distance, string Duration)>();
            var sourceGeoPoint = new GeoPoint2d(sourceX, sourceY);
            var request = new DirectionsRequest
            {
                Annotations = [DirectionsAnnotationType.Distance, DirectionsAnnotationType.Duration],
                Profile = RoutingProfile.Driving,
                Overview = OverviewType.Full,
                Alternatives = true,
                AccessToken = "YOUR_MAPBOX_ACCESS_TOKEN"
            };

            var tasks = destinations.Select(async (destination, i) =>
            {
                request.Waypoints = [destination, sourceGeoPoint];
                var response = await _mapboxService.GetDirectionsAsync(request);
                if (response == null) return;
                if (response.Routes.Count > 1)
                    response.Routes.Sort((r1, r2) => r1.Duration.CompareTo(r2.Duration));
                var route = response.Routes[0];
                distances[schools[i].Name] = (MetersToMiles(route.Distance), $"{route.Duration}|{FormatTimeSpan(TimeSpan.FromSeconds(route.Duration))}");
            });
            await Task.WhenAll(tasks);
            return distances;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetDistances: {ex.Message}");
            return [];
        }
    }

    private static double MetersToMiles(double meters) => meters / 1609.34;
    private static string FormatTimeSpan(TimeSpan timeSpan) => $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
}

public record BoundingBox
{
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLng { get; }
    public double MaxLng { get; }

    public BoundingBox(List<GeoPoint2d> latLngs)
    {
        if (latLngs.Count == 0)
            throw new ArgumentException("LatLngs list cannot be empty", nameof(latLngs));

        MinLat = latLngs.Min(latLng => latLng.Lat);
        MaxLat = latLngs.Max(latLng => latLng.Lat);
        MinLng = latLngs.Min(latLng => latLng.Lon);
        MaxLng = latLngs.Max(latLng => latLng.Lon);
    }
}
