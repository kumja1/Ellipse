using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Snapping.OpenRoute;

public class OpenRouteSnappingRequest
{
    [JsonPropertyName("locations")]
    public double[][] Locations { get; set; } = [];

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("radius")]
    public int Radius { get; set; }
}
