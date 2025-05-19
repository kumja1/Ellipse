using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Matrix.OpenRoute;

public class OpenRouteMatrixResponse
{
    [JsonPropertyName("durations")]
    public double[][]? Durations { get; set; }

    [JsonPropertyName("distances")]
    public double[][]? Distances { get; set; }
}
