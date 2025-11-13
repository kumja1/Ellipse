using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class Geometry
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("coordinates")]
    public required List<double> Coordinates { get; set; }
}
