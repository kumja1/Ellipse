using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding;

public class TigerLine
{
    [JsonPropertyName("side")]
    public string Side { get; set; }

    [JsonPropertyName("tigerLineId")]
    public string TigerLineId { get; set; }
}
