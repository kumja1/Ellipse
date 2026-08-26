using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Matrix.OpenRoute;

public record OpenRouteMatrixResponse
{
    [JsonPropertyName("durations")]
    public float?[][]? Durations { get; set; }

    [JsonPropertyName("distances")]
    public float?[][]? Distances { get; set; }

    public bool IsValid => Durations != null && !Durations.Contains(null); // Only need to check one of the arrays for validity
}
