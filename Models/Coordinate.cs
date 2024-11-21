namespace Ellipse.Models;

public struct PointInfo
{
    public Coordinate? Coordinate { get; set; }
    public double Average { get; set; }

    public void Clear()
    {
        Coordinate = null;
        Average = 0;
    }
}

