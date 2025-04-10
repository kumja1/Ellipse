using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding;

/// <summary>
/// Models the overall response returned by the Census Geocoder API.
/// </summary>
public class GeocodingResponse
{
    [JsonPropertyName("result")]
    public Result Result { get; set; }
}
