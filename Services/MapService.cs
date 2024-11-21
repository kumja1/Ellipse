using System.Text.Json;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using dotnet80_example.Models;
using GeoCalculator = Geolocation.GeoCalculator;

namespace Ellipse.Services;
public class MapService(HttpClient httpClient)
{
    private readonly HttpClient Http = httpClient;
    private readonly ObjectPool<PointInfo> pointInfoPool = new(() => new PointInfo());

    private async Task<List<County>> FetchCounties()
    {
        var result = JsonConvert.DeserializeObject<RequestResult>(await Http.GetStringAsync("https://public.opendatasoft.com/api/explore/v2.1/catalog/datasets/us-county-boundaries/records?select=namelsad%2Cgeo_point_2d&where=search(namelsad%2C%27Amelia%20County%27)%20OR%20search(namelsad%2C%27Charles%20City%20County%27)%20OR%20search(namelsad%2C%27Chesterfield%20County%27)%20OR%20search(namelsad%2C%27Colonial%20Heights%20City%27)%20OR%20search(namelsad%2C%27Cumberland%20County%27)%20OR%20search(namelsad%2C%27Dinwiddie%20County%27)%20OR%20search(namelsad%2C%27Hanover%20County%27)%20OR%20search(namelsad%2C%27Henrico%20County%27)%20OR%20search(namelsad%2C%27Hopewell%20City%27)%20OR%20search(namelsad%2C%27New%20Kent%20County%27)%20OR%20search(namelsad%2C%27Petersburg%20City%27)%20OR%20search(namelsad%2C%27Powhatan%20County%27)%20OR%20search(namelsad%2C%27Prince%20George%20County%27)%20OR%20search(namelsad%2C%27Richmond%20City%27)%20OR%20search(namelsad%2C%27Sussex%20County%27)&limit=20&refine=state_name%3A%22Virginia%22"));
        ArgumentNullException.ThrowIfNull(result);

        if (result.Results != null && result.Results.Count > 0)
        {
            result.Results.RemoveAll(county => county.Name == "Essex County");
        }

        return result.Results ?? [];
    }

    public async Task<List<(string Name, Coordinate Coordinate, double AverageDistance)>> GetAverageDistances()
    {
        var counties = await FetchCounties();
        var boundingBox = new BoundingBox(counties);

        var step = 0.1;
        Dictionary<string, PointInfo> distances = new(15);
        for (var x = boundingBox.MinLat; x < boundingBox.MaxLat; x += step)
        {
            for (var y = boundingBox.MinLng; y < boundingBox.MaxLng; y += step)
            {
                double totalDistance = 0;
                var pointInfo = pointInfoPool.Get();
                pointInfo.Coordinate = new Coordinate(x, y);
                foreach (var county in counties)
                {
                    var distance = GeoCalculator.GetDistance(county.LatLng.Lat, county.LatLng.Lng, x, y);
                    totalDistance += distance;
                }

                pointInfo.Average = totalDistance / counties.Count;
                distances[$"{x},{y}"] = pointInfo;
                pointInfo.Clear();
                pointInfoPool.Return(pointInfo);
            }
        }

        return distances.Select(obj => (Name: obj.Key, Coordinate: (Coordinate)obj.Value.Coordinate, AverageDistance: obj.Value.Average)).ToList();
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


public readonly struct BoundingBox(List<County> counties)
{
    public double MinLat { get; } = counties.Min(county => county.LatLng.Lat);
    public double MaxLat { get; } = counties.Max(county => county.LatLng.Lat);
    public double MinLng { get; } = counties.Min(county => county.LatLng.Lng);
    public double MaxLng { get; } = counties.Max(county => county.LatLng.Lng);
}

public class ObjectPool<T>
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectGenerator;

    public ObjectPool(Func<T> objectGenerator)
    {
        ArgumentNullException.ThrowIfNull(objectGenerator);
        _objectGenerator = objectGenerator;
        _objects = [];
    }

    public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

    public void Return(T item) => _objects.Add(item);
}