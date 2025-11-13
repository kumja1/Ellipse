using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class Benchmark
{
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("benchmarkDescription")]
    public required string BenchmarkDescription { get; set; }

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("benchmarkName")]
    public required string BenchmarkName { get; set; }
}
