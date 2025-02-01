using Ellipse.Enums;

namespace Ellipse.Models;

public class MatrixRequest
{


    /// <summary>
    /// Gets or sets the routing profile. 
    /// </summary>
    public RoutingProfile Profile { get; set; } = RoutingProfile.Driving;

    public List<GeoPoint2d> Sources { get; set; } = [];

    public List<GeoPoint2d> Destinations { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional annotations. Options include 'duration', 'distance', or both.
    /// </summary>
    public List<MatrixAnnotationType>? Annotations { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional approaches for each waypoint.
    /// </summary>
    public List<Approach>? Approaches { get; set; }

    /// <summary>
    /// Gets or sets the optional bearings for each waypoint.
    /// </summary>
    public List<Bearing>? Bearings { get; set; }

    /// <summary>
    /// Gets or sets the optional waypoints' names.
    /// </summary>
    public List<string>? WaypointNames { get; set; }
}


