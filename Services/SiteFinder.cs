using Mapbox.AspNetCore.Models;
using MapboxGeocoder = Mapbox.AspNetCore.Services.MapBoxService;
using OpenLayers.Blazor;
using GeoPoint2d = Ellipse.Models.GeoPoint2d;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Ellipse.Enums;


namespace Ellipse.Services;

public class SiteFinder(HttpClient httpClient, MapboxGeocoder geocoder, MapboxClient mapboxService)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly MapboxGeocoder _geocoder = geocoder;

    private readonly MapboxClient _mapboxService = mapboxService;

    private readonly Dictionary<string, string> _addressCache = [];

    private const double STEP_SIZE = 0.1; // 0.1 degrees is approximately 10 km

    private async Task<List<County>> FetchCounties()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<RequestResult>(
                "https://public.opendatasoft.com/api/explore/v2.1/catalog/datasets/us-county-boundaries/records?" +
                "select=namelsad%2Cgeo_point_2d&" +
                "where=search(namelsad%2C%27Amelia%20County%27)%20OR%20search(namelsad%2C%27Charles%20City%20County%27)%20OR%20" +
                "search(namelsad%2C%27Chesterfield%20County%27)%20OR%20search(namelsad%2C%27Colonial%20Heights%20City%27)%20OR%20" +
                "search(namelsad%2C%27Cumberland%20County%27)%20OR%20search(namelsad%2C%27Dinwiddie%20County%27)%20OR%20" +
                "search(namelsad%2C%27Hanover%20County%27)%20OR%20search(namelsad%2C%27Henrico%20County%27)%20OR%20" +
                "search(namelsad%2C%27Hopewell%20City%27)%20OR%20search(namelsad%2C%27New%20Kent%20County%27)%20OR%20" +
                "search(namelsad%2C%27Petersburg%20City%27)%20OR%20search(namelsad%2C%27Powhatan%20County%27)%20OR%20" +
                "search(namelsad%2C%27Prince%20George%20County%27)%20OR%20search(namelsad%2C%27Richmond%20City%27)%20OR%20" +
                "search(namelsad%2C%27Sussex%20County%27)&limit=20&refine=state_name%3A%22Virginia%22");


            if (response?.Results == null)
                return [];

            response.Results.RemoveAll(county => county.Name == "Essex County");
            return response.Results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching counties: {ex.Message}");
            return [];
        }
    }

    public async IAsyncEnumerable<Marker> GetMarkers()
    {

        var counties = await FetchCounties();
        if (counties.Count == 0)
            yield break;

        var latLngs = counties.Select(county => county.LatLng).ToList();
        //  Console.WriteLine($"Counties: {counties.Count}");
        //  Console.WriteLine($"LatLngs: {latLngs.Count}");
        //  Console.WriteLine($"Info: {counties[0].Name} {counties[0].LatLng}:{latLngs[0]}, {counties[1].Name} {counties[1].LatLng}");

        var boundingBox = new BoundingBox(latLngs);


        foreach (var batch in GenerateGrid(boundingBox).Chunk(10))
        {
            var tasks = batch.Select(async p => await GetMarker(counties, latLngs, p));
            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result != null)
                    yield return result;
            }
        }
    }


    async Task<Marker?> GetMarker(List<County> counties, List<GeoPoint2d> latLngs, (double x, double y) point)
    {
        var (x, y) = point;
        var distances = await GetDistances(counties, latLngs, x, y);
        if (distances.Count == 0)
            return null;

        var triemeanDistance = Trimean([.. distances.Values.Select(d => d.Distance)]);
        var triemeanDuration = TimeSpan.FromSeconds(Trimean([.. distances.Values.Select(d => double.Parse(d.Duration.Split("|")[0].Trim()))]));

        distances["Average Distance"] = (triemeanDistance, FormatTimeSpan(triemeanDuration));
        return new Marker(MarkerType.MarkerPin, new Coordinate(y, x), null, PinColor.Blue)
        {
            Properties =
                {
                    ["Name"] = await GetAddressCached(x, y),
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

        /// <summary>
        /// Computes quartiles using a more precise weighted ranking method.
        /// </summary>
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
    }

    private static IEnumerable<(double x, double y)> GenerateGrid(BoundingBox boundingBox)
    {
        for (var x = boundingBox.MinLat; x <= boundingBox.MaxLat; x += STEP_SIZE)
            for (var y = boundingBox.MinLng; y <= boundingBox.MaxLng; y += STEP_SIZE)
                yield return (x, y);
    }

    private async Task<string> GetAddressCached(double latitude, double longitude)
    {
        var key = $"{latitude},{longitude}";
        if (_addressCache.TryGetValue(key, out var cachedAddress))
            return cachedAddress;

        var address = await GetAddress(latitude, longitude);
        _addressCache[key] = address;
        return address;
    }

    private async Task<string> GetAddress(double latitude, double longitude)
    {
        try
        {
            var response = await _geocoder.ReverseGeocodingAsync(new ReverseGeocodingParameters
            {
                Coordinates = new GeoCoordinate
                {
                    Latitude = latitude,
                    Longitude = longitude
                }
            });

            return response?.Place?.Name ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting address: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<Dictionary<string, (double Distance, string Duration)>> GetDistances(List<County> counties, List<GeoPoint2d> destinations, double sourceX, double sourceY)
    {
        try
        {
            var distances = new Dictionary<string, (double Distance, string Duration)>();
            var sourceGeoPoint = new GeoPoint2d(sourceX, sourceY);
            var request = new Models.DirectionsRequest
            {

                Annotations = [
                   DirectionsAnnotationType.Distance,
                    DirectionsAnnotationType.Duration
               ],
                Profile = RoutingProfile.Driving,
                Overview = OverviewType.Full,
                Alternatives = true,
                AccessToken = "pk.eyJ1Ijoia3VtamExIiwiYSI6ImNtMmRoenRsaDEzY3cyam9uZDA1cThzeDIifQ.twiBonW5YmTeLXjMEBhccA"
            };

            var tasks = destinations.Select(async (destination, i) =>
            {
                request.Waypoints = [destination, sourceGeoPoint];

               // Console.WriteLine($" Name: {counties[i].Name}, LatLng:{counties[i].LatLng}, LatLngLat:{destination}, Source:({sourceY},{sourceX}),Requesting {i + 1}/{destinations.Count}... ");
                var response = await _mapboxService.GetDirectionsAsync(request);

                if (response == null)
                    return;

                if (response.Routes.Count > 1)
                {
                    Console.WriteLine($"Multiple routes found for {counties[i].Name}, sorting by duration...");
                    response.Routes.Sort((r1, r2) => r1.Duration.CompareTo(r2.Duration));
                }
                
                var route = response.Routes[0];

                distances[counties[i].Name] = (MetersToMiles(response.Routes[0].Distance), $"{route.Duration}|{FormatTimeSpan(TimeSpan.FromSeconds(route.Duration))}");
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

public record RequestResult(List<County> Results);
public record County
{
    [JsonPropertyName("geo_point_2d")]
    public required GeoPoint2d LatLng { get; init; }

    [JsonPropertyName("namelsad")]
    public required string Name { get; init; }
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
