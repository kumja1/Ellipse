using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Snapping.OpenRoute;

public class SnappedLocation
{
    [JsonPropertyName("location")]
    public double[]? Location { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("snapped_distance")]
    public double? SnappedDistance { get; set; }
}