using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding;

public class AddressComponents
{
    [JsonPropertyName("zip")]
    public string Zip { get; set; }

    [JsonPropertyName("streetName")]
    public string StreetName { get; set; }

    [JsonPropertyName("preType")]
    public string PreType { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("preDirection")]
    public string PreDirection { get; set; }

    [JsonPropertyName("suffixDirection")]
    public string SuffixDirection { get; set; }

    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("suffixType")]
    public string SuffixType { get; set; }

    [JsonPropertyName("toAddress")]
    public string ToAddress { get; set; }

    [JsonPropertyName("suffixQualifier")]
    public string SuffixQualifier { get; set; }

    [JsonPropertyName("preQualifier")]
    public string PreQualifier { get; set; }
}
