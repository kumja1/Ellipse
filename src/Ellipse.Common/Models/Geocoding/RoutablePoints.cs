namespace Ellipse.Common.Models.Geocoding;

public class RoutablePoints
{
    public List<RoutablePoint> Points { get; set; }
}


public class RoutablePoint
{
    public List<GeoPoint2d> Coordinates { get; set; }
}
