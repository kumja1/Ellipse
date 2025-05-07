using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class Result
{
    [JsonPropertyName("input")]
    public Input Input { get; set; }

    [JsonPropertyName("addressMatches")]
    public List<AddressMatch> AddressMatches { get; set; }

    /// <summary>
    /// Additional geographic information (only if requested).
    /// The key is the geography layer name.
    /// </summary>
    [JsonPropertyName("geographies")]
    public Dictionary<string, List<Geography>> Geographies { get; set; }
}
