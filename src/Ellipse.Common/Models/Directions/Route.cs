
namespace Ellipse.Common.Models.Directions;

public class Route

{
    public string Geometry { get; set; }
    public List<Leg> Legs { get; set; } = [];
    public double Duration { get; set; }
    public double Distance { get; set; }
}