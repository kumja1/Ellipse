using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

/// <summary>
/// Represents a single geography feature returned (for example, a Census Block Group).
/// </summary>
public class Geography
{
    [JsonPropertyName("GEOID")]
    public required string GEOID { get; set; }

    [JsonPropertyName("CENTLAT")]
    public required string CENTLAT { get; set; }

    /// <summary>
    /// Area of water, if provided.
    /// </summary>
    [JsonPropertyName("AREAWATER")]
    public int? AREAWATER { get; set; }

    [JsonPropertyName("STATE")]
    public required string STATE { get; set; }

    [JsonPropertyName("BASENAME")]
    public required string BASENAME { get; set; }

    [JsonPropertyName("OID")]
    public required string OID { get; set; }

    [JsonPropertyName("LSADC")]
    public required string LSADC { get; set; }

    [JsonPropertyName("FUNCSTAT")]
    public required string FUNCSTAT { get; set; }

    [JsonPropertyName("INTPTLAT")]
    public required string INTPTLAT { get; set; }

    [JsonPropertyName("NAME")]
    public required string NAME { get; set; }

    [JsonPropertyName("OBJECTID")]
    public int? OBJECTID { get; set; }

    [JsonPropertyName("TRACT")]
    public required string TRACT { get; set; }

    [JsonPropertyName("CENTLON")]
    public required string CENTLON { get; set; }

    [JsonPropertyName("BLKGRP")]
    public required string BLKGRP { get; set; }

    /// <summary>
    /// Area of land, if provided.
    /// </summary>
    [JsonPropertyName("AREALAND")]
    public int? AREALAND { get; set; }

    [JsonPropertyName("INTPTLON")]
    public required string INTPTLON { get; set; }

    [JsonPropertyName("MTFCC")]
    public required string MTFCC { get; set; }

    [JsonPropertyName("COUNTY")]
    public required string COUNTY { get; set; }
}
