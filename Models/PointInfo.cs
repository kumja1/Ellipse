using Newtonsoft.Json;

namespace Ellipse.Models;

public struct Coordinate(double Lat, double Lng)
{
    [JsonProperty("lon")]
    public double Lng { get; set; } = Lng;

    [JsonProperty("lat")]
    public double Lat { get; set; } = Lat;
}