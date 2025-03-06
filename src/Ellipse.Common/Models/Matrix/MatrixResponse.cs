using System.Text.Json.Serialization;
using Ellipse.Common.Models.Directions;

namespace Ellipse.Common.Models.Matrix;

public class MatrixResponse
{
    /// <summary>
    /// Gets or sets the status code of the response.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the durations matrix.
    /// </summary>
    [JsonPropertyName("durations")]
    public List<List<double>>? Durations { get; set; }

    /// <summary>
    /// Gets or sets the distances matrix.
    /// </summary>
    [JsonPropertyName("distances")]
    public List<List<double>>? Distances { get; set; }

    /// <summary>
    /// Gets or sets the list of source waypoints.
    /// </summary>
    [JsonPropertyName("sources")]
    public List<Waypoint>? Sources { get; set; }

    /// <summary>
    /// Gets or sets the list of destination waypoints.
    /// </summary>
    [JsonPropertyName("destinations")]
    public List<Waypoint>? Destinations { get; set; }
}
