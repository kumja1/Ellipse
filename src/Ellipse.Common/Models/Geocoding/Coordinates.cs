using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding;

public class Coordinates
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}
