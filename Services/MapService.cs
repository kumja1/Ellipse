using System.Text.Json;
using Ellipse.Models;
using GeoCalculator = Geolocation.GeoCalculator;
using OpenCage.Geocode;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace Ellipse.Services;

public class MapService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly GeoCoder _geocoder = new("de089c7b941546b1acb0f8bea495b5c9");

    private async Task<List<County>> FetchCounties()
    {
        var result = await _httpClient.GetFromJsonAsync<RequestResult>("https://public.opendatasoft.com/api/explore/v2.1/catalog/datasets/us-county-boundaries/records?select=namelsad%2Cgeo_point_2d&where=search(namelsad%2C%27Amelia%20County%27)%20OR%20search(namelsad%2C%27Charles%20City%20County%27)%20OR%20search(namelsad%2C%27Chesterfield%20County%27)%20OR%20search(namelsad%2C%27Colonial%20Heights%20City%27)%20OR%20search(namelsad%2C%27Cumberland%20County%27)%20OR%20search(namelsad%2C%27Dinwiddie%20County%27)%20OR%20search(namelsad%2C%27Hanover%20County%27)%20OR%20search(namelsad%2C%27Henrico%20County%27)%20OR%20search(namelsad%2C%27Hopewell%20City%27)%20OR%20search(namelsad%2C%27New%20Kent%20County%27)%20OR%20search(namelsad%2C%27Petersburg%20City%27)%20OR%20search(namelsad%2C%27Powhatan%20County%27)%20OR%20search(namelsad%2C%27Prince%20George%20County%27)%20OR%20search(namelsad%2C%27Richmond%20City%27)%20OR%20search(namelsad%2C%27Sussex%20County%27)&limit=20&refine=state_name%3A%22Virginia%22");
        ArgumentNullException.ThrowIfNull(result);

        result.Results?.RemoveAll(county => county.Name == "Essex County");
        return result.Results ?? [];
    }

    public async Task<List<PointInfo>> GetAverageDistances()
    { 
        try {
        var counties = await FetchCounties();
        var boundingBox = new BoundingBox(counties);

        var step = 100;
        List<PointInfo> distances = [];

        for (var x = boundingBox.MinLat; x < boundingBox.MaxLat; x += step)
        {
            for (var y = boundingBox.MinLng; y < boundingBox.MaxLng; y += step)
            {
                double totalDistance = 0;
                foreach (var county in counties)
                {
                    var distance = GeoCalculator.GetDistance(county.LatLng.Lat, county.LatLng.Lng, x, y);
                    totalDistance += distance;
                }
                var pointInfo = new PointInfo
                {
                    Latitude = x,
                    Longitude = y,
                    Average = totalDistance / counties.Count,
                    Name = await GetAddress(x, y).ConfigureAwait(false)
                };
                Console.WriteLine($"Latitude: {pointInfo.Latitude}, Longitude: {pointInfo.Longitude}, Average: {pointInfo.Average}, Name: {pointInfo.Name}");
                distances.Add(pointInfo);
            }
        }

        distances.Sort((a, b) => a.Average.CompareTo(b.Average));
        return distances;
        }catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return [];
    }


    public async Task<string> GetAddress(double latitude, double longitude)
    {
        try
        {
            var response = await _geocoder.ReverseGeoCodeAsync(latitude, longitude);
            return response.Results.First().Formatted;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        return string.Empty;
    }
}

public class RequestResult
{
    [JsonProperty("results")]
    public List<County> Results { get; set; } = [];
}

public class County
{
    [JsonProperty("geo_point_2d")]
    public Coordinate LatLng { get; set; }

    [JsonProperty("namelsad")]
    public string Name { get; set; } = string.Empty;
}

public class BoundingBox(List<County> counties)
{
    public double MinLat { get; } = counties.Min(county => county.LatLng.Lat);
    public double MaxLat { get; } = counties.Max(county => county.LatLng.Lat);
    public double MinLng { get; } = counties.Min(county => county.LatLng.Lng);
    public double MaxLng { get; } = counties.Max(county => county.LatLng.Lng);
}

public struct Coordinate
{
    [JsonProperty("lon")]
    public double Lng { get; set; }

    [JsonProperty("lat")]
    public double Lat { get; set; }
}