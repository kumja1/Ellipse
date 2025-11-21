using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class Feature
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry Geometry { get; set; }

    [JsonPropertyName("properties")]
    public Properties Properties { get; set; }

    [JsonPropertyName("bbox")]
    public List<double> BoundingBox { get; set; }
}

public class GeocodeResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("features")]
    public List<Feature> Features { get; set; }

    [JsonPropertyName("bbox")]
    public List<double> BoundingBox { get; set; }
}
