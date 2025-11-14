using System.Text.Json.Serialization;
using Ellipse.Common.Enums.Geocoding;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

/// <summary>
/// Base request model encapsulating properties common to all types of geocoding requests.
/// </summary>
public record CensusGeocodingRequest
{
    [JsonPropertyName("returntype")]
    public ReturnType ReturnType { get; init; }

    [JsonPropertyName("searchtype")]
    public SearchType SearchType { get; init; }

    [JsonPropertyName("benchmark")]
    public string Benchmark { get; init; }

    /// <summary>
    /// Only required if ReturnType equals Geographies.
    /// </summary>
    [JsonPropertyName("vintage")]
    public required string Vintage { get; init; }

    // Optional parameters

    /// <summary>
    /// Controls the output format. Valid values: "json", "jsonp"
    /// </summary>
    [JsonPropertyName("format")]
    public ResponseFormat Format { get; init; } = ResponseFormat.Json;

    /// <summary>
    /// Only used if Format is init to Jsonp.
    /// </summary>
    [JsonPropertyName("callback")]
    public  string Callback { get; init; }

    /// <summary>
    /// Optional comma-delimited list of layer IDs/names.
    /// </summary>
    [JsonPropertyName("layers")]
    public string Layers { get; init; }

    // For the 'onelineaddress' search type.
    [JsonPropertyName("address")]
    public required string Address { get; init; }

    // For structured address (searchType = Address)
    [JsonPropertyName("street")]
    public string Street { get; init; }

    [JsonPropertyName("city")]
    public string City { get; init; }

    [JsonPropertyName("state")]
    public string State { get; init; }

    [JsonPropertyName("zip")]
    public string Zip { get; init; }

    // For Puerto Rico structured addresses (searchType = AddressPR)
    [JsonPropertyName("urb")]
    public string Urb { get; init; }

    [JsonPropertyName("municipio")]
    public string Municipio { get; init; }

    // For coordinate-based reverse requests (searchType = Coordinates)
    [JsonPropertyName("x")]
    public double? X { get; init; }

    [JsonPropertyName("y")]
    public double? Y { get; init; }
}
