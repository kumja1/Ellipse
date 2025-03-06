namespace Ellipse.Common.Models.Directions;

public class Leg
{
    public double Duration { get; set; }
    public double Distance { get; set; }
    public List<Step>? Steps { get; set; }
}