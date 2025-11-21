using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

/// <summary>
/// Models the overall response returned by the Census Geocoder API.
/// </summary>
public class CensusGeocodingResponse
{
    [JsonPropertyName("result")]
    public Result Result { get; set; }
}
