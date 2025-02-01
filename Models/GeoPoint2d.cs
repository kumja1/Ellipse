namespace Ellipse.Models;

public struct GeoPoint2d(double lat,double lon)
{
    public double Lon { get; set; } = lon;
    public double Lat { get; set; } = lat;
    public override readonly string ToString() => $"{Lon},{Lat}";
}