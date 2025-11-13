using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class OpenRouteGeocodingResponse
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("features")]
    public required List<Feature> Features { get; set; }

    [JsonPropertyName("bbox")]
    public required List<double> BoundingBox { get; set; }
}
