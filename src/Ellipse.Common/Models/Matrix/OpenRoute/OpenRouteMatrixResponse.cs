using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Matrix.OpenRoute;

public class OpenRouteMatrixResponse
{
    [JsonPropertyName("durations")]
    public float[][]? Durations { get; set; }

    [JsonPropertyName("distances")]
    public float[][]? Distances { get; set; }
}
