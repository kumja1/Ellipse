using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class Properties
{
    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("country")]
    public required string Country { get; set; }

    [JsonPropertyName("city")]
    public required string City { get; set; }

    [JsonPropertyName("postcode")]
    public required string Postcode { get; set; }

    [JsonPropertyName("street")]
    public required string Street { get; set; }

    [JsonPropertyName("housenumber")]
    public required string HouseNumber { get; set; }

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("gid")]
    public required string Gid { get; set; }

    [JsonPropertyName("layer")]
    public required string Layer { get; set; }

    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("source_id")]
    public required string SourceId { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("accuracy")]
    public required string Accuracy { get; set; }

    [JsonPropertyName("country_gid")]
    public required string CountryGid { get; set; }

    [JsonPropertyName("country_a")]
    public required string CountryA { get; set; }

    [JsonPropertyName("region")]
    public required string Region { get; set; }

    [JsonPropertyName("region_gid")]
    public required string RegionGid { get; set; }

    [JsonPropertyName("region_a")]
    public required string RegionA { get; set; }

    [JsonPropertyName("county")]
    public required string County { get; set; }

    [JsonPropertyName("county_gid")]
    public required string CountyGid { get; set; }

    [JsonPropertyName("county_a")]
    public required string CountyA { get; set; }

    [JsonPropertyName("continent")]
    public required string Continent { get; set; }

    [JsonPropertyName("continent_gid")]
    public required string ContinentGid { get; set; }
}
