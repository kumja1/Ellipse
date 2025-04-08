namespace Ellipse.Common.Models;

public readonly struct GeoPoint2d(double lat, double lon)
{
    public GeoPoint2d(decimal lat, decimal lon) : this((double)lat, (double)lon)
    {
    }

    public double Lon { get; init; } = lon;
    public double Lat { get; init; } = lat;
    public override readonly string ToString() => $"{Lon},{Lat}";

    public static implicit operator GeoPoint2d(ValueTuple<double, double> tuple) => new(tuple.Item1, tuple.Item2);
    public static implicit operator GeoPoint2d(ValueTuple<decimal, decimal> tuple) => new((double)tuple.Item1, (double)tuple.Item2);

    public static bool operator ==(GeoPoint2d left, GeoPoint2d right) => left.Equals(right);
    public static bool operator !=(GeoPoint2d left, GeoPoint2d right) => !left.Equals(right);

    public static GeoPoint2d Zero => new(0d, 0d);
}