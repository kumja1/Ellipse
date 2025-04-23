namespace Ellipse.Common.Models;

public readonly record struct GeoPoint2d(double Lon, double Lat)
{

    public override string ToString() => $"{Lon},{Lat}";

    public static implicit operator GeoPoint2d(ValueTuple<double, double> tuple) =>
        new(tuple.Item1, tuple.Item2);

    public static implicit operator GeoPoint2d(ValueTuple<decimal, decimal> tuple) =>
        new((double)tuple.Item1, (double)tuple.Item2);

    public static GeoPoint2d Zero => new(0d, 0d);
}
