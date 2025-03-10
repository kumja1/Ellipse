namespace Ellipse.Common.Models;

public struct GeoPoint2d(double lat, double lon)
{
    public double Lon { get; set; } = lon;
    public double Lat { get; set; } = lat;
    public override readonly string ToString() => $"{Lon},{Lat}";

    public static implicit operator GeoPoint2d(ValueTuple<double, double> tuple) => new(tuple.Item1, tuple.Item2);

    public static GeoPoint2d Zero => new(0, 0);
}