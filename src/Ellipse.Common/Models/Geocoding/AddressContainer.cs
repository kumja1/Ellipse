using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding;

public class AddressContainer
{
    [JsonPropertyName("address")]
    public string Address { get; set; }
}
