using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Directions;

public class Waypoint
{
    /// <summary>
    /// Gets or sets the distance from the network in meters.
    /// </summary>
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    /// <summary>
    /// Gets or sets the name of the waypoint.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the location of the waypoint.
    /// </summary>
    [JsonPropertyName("location")]
    public List<double> Location { get; set; }
}
