using System.Text.Json.Serialization;

public class SnappedLocation
{
    [JsonPropertyName("location")]
    public double[]? Location { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("snapped_distance")]
    public double? SnappedDistance { get; set; }
}
