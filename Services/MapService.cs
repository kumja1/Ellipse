using System.Text.Json;
using Mapbox.AspNetCore.Models;
using Mapbox.AspNetCore.Services;
using OpenLayers.Blazor;
using GeoPoint2d = Ellipse.Models.GeoPoint2d;
using GoogleMapServices.Models;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using GoogleMapServices.Enums;
using GoogleMapServices;
using System.Collections;


namespace Ellipse.Services;

public class MapService
{
    private readonly HttpClient _httpClient;
    private readonly MapBoxService _geocoder;

    // Cache for geocoding results
    private readonly Dictionary<string, string> _addressCache = [];

    // Configurable parameters
    private const double STEP_SIZE = 0.1;
    private const int BATCH_SIZE = 20;

    private readonly SemaphoreSlim _semaphore = new(10, 60);

    public MapService(HttpClient httpClient, MapBoxService geocoder)
    {
        _httpClient = httpClient;
        _geocoder = geocoder;
    }

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

   public async Task<IList<Marker>> GetAverageDistances()
{
    var counties = await FetchCounties();
    if (counties.Count == 0)
        return new List<Marker>();

    var boundingBox = new BoundingBox(counties);
    var latLngs = counties.Select(county => county.LatLng).ToList();

    var allMarkers = new List<Marker>();

    foreach (var batch in GenerateGrid(boundingBox).Chunk(BATCH_SIZE))
    {
        try
        {
            await _semaphore.WaitAsync();
            var tasks = batch.Select(async point =>
            {
                var (x, y) = point;

                var distances = await GetDistances(counties, latLngs, x, y);
                if (distances.Count == 0)
                    return null;

                var sortedDistances = distances.Values.OrderBy(d => d.Distance).ToList();
                var medianDistance = sortedDistances.Count % 2 == 0
                    ? (sortedDistances[sortedDistances.Count / 2 - 1].Distance + sortedDistances[sortedDistances.Count / 2].Distance) / 2
                    : sortedDistances[sortedDistances.Count / 2].Distance;

                var durations = distances.Values
                    .Select(d => double.Parse(d.Duration.Split("|")[0].Trim()))
                    .OrderBy(d => d)
                    .ToList();

                var medianDuration = GetMedianTime(durations);

                distances["Median Distance"] = (medianDistance, $"{(int)medianDuration / 3600}h {(int)(medianDuration % 3600) / 60}m");

                return new Marker(MarkerType.MarkerPin, new Coordinate(y, x), null, PinColor.Green)
                {
                    Properties = {
                        {"Name", await GetAddressCached(x, y)},
                        {"Distances", distances}
                    },
                    Scale = 0.1,
                    Popup = true,
                };
            });

            var markers = await Task.WhenAll(tasks);
            allMarkers.AddRange(markers);
        }
        finally
        {
            _semaphore.Release();
        }

        await Task.Delay(TimeSpan.FromSeconds(60));
    }

    return allMarkers;
}



    private double GetMedianTime(List<double> durations)
    {
        if (durations.Count % 2 == 1)
            return durations[durations.Count / 2];
        return (durations[durations.Count / 2 - 1] + durations[durations.Count / 2]) / 2.0;
    }

    private IEnumerable<(double x, double y)> GenerateGrid(BoundingBox boundingBox)
    {
        for (var x = boundingBox.MinLat; x <= boundingBox.MaxLat; x += STEP_SIZE)
        {
            for (var y = boundingBox.MinLng; y <= boundingBox.MaxLng; y += STEP_SIZE)
            {
                yield return (x, y);
            }
        }
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

            var request = new DistanceMatrixRequest
            {

                Key = "AIzaSyDDUJKLZhoqKi5oh2tJGtFjKip6kguELY4",
                Origins = [
                    new() {
                        LatLongPair = new LatLongPair {
                            Latitude = sourceX,
                            Longitude = sourceY
                        }
                    }
                ],
                Destinations = destinations.Select(d => new LocationParameter
                {
                    LatLongPair = new LatLongPair
                    {
                        Latitude = d.Lat,
                        Longitude = d.Lon
                    }
                }).ToList(),
                TravelMode = TravelModes.driving,

            };

            var url = $"https://corsproxy.io/?url={Uri.EscapeDataString($"https://maps.googleapis.com/maps/api/distancematrix/json?{request}")}";


            var response = await _httpClient.GetFromJsonAsync<DistanceMatrixResponse>(url, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new MatrixstatusConverter(), new ElementLevelStatusConverter() } });
            for (var i = 0; i < response.Rows[0].Elements.Count; i++)
            {
                var matrix = response.Rows[0].Elements[i];
                distances.Add(counties[i].Name, (MetersToMiles(matrix.Distance.Value), $"{matrix.Duration.Value} | {matrix.Duration.Text}"));
            }

            return distances;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetDistances: {ex.Message}");
            return [];
        }
    }

    private static double MetersToMiles(double meters) => meters * 0.000621371;
}

public record RequestResult
{
    public List<County> Results { get; init; } = [];
}

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

    public BoundingBox(List<County> counties)
    {
        if (!counties.Any())
            throw new ArgumentException("Counties list cannot be empty", nameof(counties));

        MinLat = counties.Min(county => county.LatLng.Lat);
        MaxLat = counties.Max(county => county.LatLng.Lat);
        MinLng = counties.Min(county => county.LatLng.Lon);
        MaxLng = counties.Max(county => county.LatLng.Lon);
    }
}

public class MatrixstatusConverter : JsonConverter<Matrixstatus>
{
    public override Matrixstatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse(value, true, out Matrixstatus result) ? result : Matrixstatus.UNKNOWN_ERROR;
    }

    public override void Write(Utf8JsonWriter writer, Matrixstatus value, JsonSerializerOptions options)
    {

        writer.WriteStringValue(value.ToString());
    }
}


public class ElementLevelStatusConverter : JsonConverter<ElementLevelStatus>
{
    public override ElementLevelStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse(value, true, out ElementLevelStatus result) ? result : ElementLevelStatus.ZERO_RESULTS;
    }

    public override void Write(Utf8JsonWriter writer, ElementLevelStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
