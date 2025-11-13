using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class Input
{
    /// <summary>
    /// For onelineaddress searches, the address is nested inside this object.
    /// </summary>
    [JsonPropertyName("address")]
    public required AddressContainer Address { get; set; }

    [JsonPropertyName("benchmark")]
    public required Benchmark Benchmark { get; set; }
}
