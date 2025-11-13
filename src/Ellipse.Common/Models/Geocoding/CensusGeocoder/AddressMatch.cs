using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class AddressMatch
{
    [JsonPropertyName("tigerLine")]
    public required TigerLine TigerLine { get; set; }

    [JsonPropertyName("coordinates")]
    public required Coordinates Coordinates { get; set; }

    [JsonPropertyName("addressComponents")]
    public required AddressComponents AddressComponents { get; set; }

    [JsonPropertyName("matchedAddress")]
    public required string MatchedAddress { get; set; }
}
