using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class Feature
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("geometry")]
    public required Geometry Geometry { get; set; }

    [JsonPropertyName("properties")]
    public required Properties Properties { get; set; }

    [JsonPropertyName("bbox")]
    public required List<double> BoundingBox { get; set; }
}

public class GeocodeResponse
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("features")]
    public required List<Feature> Features { get; set; }

    [JsonPropertyName("bbox")]
    public required List<double> BoundingBox { get; set; }
}
