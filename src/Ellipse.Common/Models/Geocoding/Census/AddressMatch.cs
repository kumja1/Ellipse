using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class AddressMatch
{
    [JsonPropertyName("tigerLine")]
    public TigerLine TigerLine { get; set; }

    [JsonPropertyName("coordinates")]
    public Coordinates Coordinates { get; set; }

    [JsonPropertyName("addressComponents")]
    public AddressComponents AddressComponents { get; set; }

    [JsonPropertyName("matchedAddress")]
    public string MatchedAddress { get; set; }
}
