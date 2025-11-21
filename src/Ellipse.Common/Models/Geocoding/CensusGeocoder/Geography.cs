using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

/// <summary>
/// Represents a single geography feature returned (for example, a Census Block Group).
/// </summary>
public class Geography
{
    [JsonPropertyName("GEOID")]
    public string GEOID { get; set; }

    [JsonPropertyName("CENTLAT")]
    public string CENTLAT { get; set; }

    /// <summary>
    /// Area of water, if provided.
    /// </summary>
    [JsonPropertyName("AREAWATER")]
    public int? AREAWATER { get; set; }

    [JsonPropertyName("STATE")]
    public string STATE { get; set; }

    [JsonPropertyName("BASENAME")]
    public string BASENAME { get; set; }

    [JsonPropertyName("OID")]
    public string OID { get; set; }

    [JsonPropertyName("LSADC")]
    public string LSADC { get; set; }

    [JsonPropertyName("FUNCSTAT")]
    public string FUNCSTAT { get; set; }

    [JsonPropertyName("INTPTLAT")]
    public string INTPTLAT { get; set; }

    [JsonPropertyName("NAME")]
    public string NAME { get; set; }

    [JsonPropertyName("OBJECTID")]
    public int? OBJECTID { get; set; }

    [JsonPropertyName("TRACT")]
    public string TRACT { get; set; }

    [JsonPropertyName("CENTLON")]
    public string CENTLON { get; set; }

    [JsonPropertyName("BLKGRP")]
    public string BLKGRP { get; set; }

    /// <summary>
    /// Area of land, if provided.
    /// </summary>
    [JsonPropertyName("AREALAND")]
    public int? AREALAND { get; set; }

    [JsonPropertyName("INTPTLON")]
    public string INTPTLON { get; set; }

    [JsonPropertyName("MTFCC")]
    public string MTFCC { get; set; }

    [JsonPropertyName("COUNTY")]
    public string COUNTY { get; set; }
}
