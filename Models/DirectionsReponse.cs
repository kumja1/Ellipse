namespace Ellipse.Models;

public class DirectionsResponse
{
    public List<Route> Routes { get; set; } = [];
    public List<Waypoint> Waypoints { get; set; } = [];
}

public class Route
{
    public string Geometry { get; set; }
    public List<Leg> Legs { get; set; } = [];
    public double Duration { get; set; }
    public double Distance { get; set; }
}

public class Leg
{
    public double Duration { get; set; }
    public double Distance { get; set; }
    public List<Step>? Steps { get; set; }
}

public class Step
{
    public string Maneuver { get; set; }
    public double Duration { get; set; }
    public double Distance { get; set; }
}
