using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public class Properties
{
    [JsonPropertyName("label")]
    public string Label { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; }

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; }

    [JsonPropertyName("street")]
    public string Street { get; set; }

    [JsonPropertyName("housenumber")]
    public string HouseNumber { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("gid")]
    public string Gid { get; set; }

    [JsonPropertyName("layer")]
    public string Layer { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; }

    [JsonPropertyName("source_id")]
    public string SourceId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("accuracy")]
    public string Accuracy { get; set; }

    [JsonPropertyName("country_gid")]
    public string CountryGid { get; set; }

    [JsonPropertyName("country_a")]
    public string CountryA { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; }

    [JsonPropertyName("region_gid")]
    public string RegionGid { get; set; }

    [JsonPropertyName("region_a")]
    public string RegionA { get; set; }

    [JsonPropertyName("county")]
    public string County { get; set; }

    [JsonPropertyName("county_gid")]
    public string CountyGid { get; set; }

    [JsonPropertyName("county_a")]
    public string CountyA { get; set; }

    [JsonPropertyName("continent")]
    public string Continent { get; set; }

    [JsonPropertyName("continent_gid")]
    public string ContinentGid { get; set; }
}
