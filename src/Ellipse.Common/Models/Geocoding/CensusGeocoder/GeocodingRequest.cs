using System.Text.Json.Serialization;
using Ellipse.Common.Enums.Geocoding;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

/// <summary>
/// Base request model encapsulating properties common to all types of geocoding requests.
/// </summary>
public class GeocodingRequest
{
    [JsonPropertyName("returntype")]
    public ReturnType ReturnType { get; set; }

    [JsonPropertyName("searchtype")]
    public SearchType SearchType { get; set; }

    [JsonPropertyName("benchmark")]
    public string Benchmark { get; set; }

    /// <summary>
    /// Only required if ReturnType equals Geographies.
    /// </summary>
    [JsonPropertyName("vintage")]
    public string Vintage { get; set; }

    // Optional parameters

    /// <summary>
    /// Controls the output format. Valid values: "json", "jsonp"
    /// </summary>
    [JsonPropertyName("format")]
    public ResponseFormat Format { get; set; } = ResponseFormat.Json;

    /// <summary>
    /// Only used if Format is set to Jsonp.
    /// </summary>
    [JsonPropertyName("callback")]
    public string Callback { get; set; }

    /// <summary>
    /// Optional comma-delimited list of layer IDs/names.
    /// </summary>
    [JsonPropertyName("layers")]
    public string Layers { get; set; }

    // For the 'onelineaddress' search type.
    [JsonPropertyName("address")]
    public string Address { get; set; }

    // For structured address (searchType = Address)
    [JsonPropertyName("street")]
    public string Street { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("zip")]
    public string Zip { get; set; }

    // For Puerto Rico structured addresses (searchType = AddressPR)
    [JsonPropertyName("urb")]
    public string Urb { get; set; }

    [JsonPropertyName("municipio")]
    public string Municipio { get; set; }

    // For coordinate-based reverse requests (searchType = Coordinates)
    [JsonPropertyName("x")]
    public double? X { get; set; }

    [JsonPropertyName("y")]
    public double? Y { get; set; }
}
