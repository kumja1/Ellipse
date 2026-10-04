using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.Pelias;

public class PeliasGeocodingResponse
{
    [JsonPropertyName("type")]
    public  string Type { get; set; }

    [JsonPropertyName("features")]
    public  List<Feature> Features { get; set; }

    [JsonPropertyName("bbox")]
    public  List<double> BoundingBox { get; set; }
}
