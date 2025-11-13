namespace Ellipse.Common.Models.Directions;

public class Step
{
    public required string Maneuver { get; set; }
    public double Duration { get; set; }
    public double Distance { get; set; }
}
