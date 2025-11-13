using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class AddressComponents
{
    [JsonPropertyName("zip")]
    public required string Zip { get; set; }

    [JsonPropertyName("streetName")]
    public required string StreetName { get; set; }

    [JsonPropertyName("preType")]
    public required string PreType { get; set; }

    [JsonPropertyName("city")]
    public required string City { get; set; }

    [JsonPropertyName("preDirection")]
    public required string PreDirection { get; set; }

    [JsonPropertyName("suffixDirection")]
    public required string SuffixDirection { get; set; }

    [JsonPropertyName("fromAddress")]
    public required string FromAddress { get; set; }

    [JsonPropertyName("state")]
    public required string State { get; set; }

    [JsonPropertyName("suffixType")]
    public required string SuffixType { get; set; }

    [JsonPropertyName("toAddress")]
    public required string ToAddress { get; set; }

    [JsonPropertyName("suffixQualifier")]
    public required string SuffixQualifier { get; set; }

    [JsonPropertyName("preQualifier")]
    public required string PreQualifier { get; set; }
}
