using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class TigerLine
{
    [JsonPropertyName("side")]
    public string Side { get; set; }

    [JsonPropertyName("tigerLineId")]
    public string TigerLineId { get; set; }
}
