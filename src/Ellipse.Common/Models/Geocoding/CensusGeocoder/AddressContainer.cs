using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class AddressContainer
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }
}
