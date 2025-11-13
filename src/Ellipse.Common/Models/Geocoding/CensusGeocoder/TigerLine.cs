using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class TigerLine
{
    [JsonPropertyName("side")]
    public required string Side { get; set; }

    [JsonPropertyName("tigerLineId")]
    public required string TigerLineId { get; set; }
}
