using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Matrix.OpenRoute;

public class Query
{
    [JsonPropertyName("locations")]
    public double[][] Locations { get; set; } = [];

    [JsonPropertyName("sources")]
    public int[]? Sources { get; set; }

    [JsonPropertyName("destinations")]
    public List<int>? Destinations { get; set; }

    [JsonPropertyName("metrics")]
    public List<Metric>? Metrics { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }

    [JsonPropertyName("profile")]
    public Profile? Profile { get; set; }
}
