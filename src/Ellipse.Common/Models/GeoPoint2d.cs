namespace Ellipse.Common.Models;

public readonly record struct GeoPoint2d(double Lon, double Lat) : IComparable<GeoPoint2d>
{
    public override string ToString() => $"{Lon},{Lat}";

    public static GeoPoint2d Zero => new(0d, 0d);

    public static bool TryParse(string str, out GeoPoint2d result)
    {
        try
        {
            result = Parse(str);
            return result != Zero;
        }
        catch
        {
            result = Zero;
            return false;
        }
    }

    public static GeoPoint2d Parse(string str)
    {
        Span<string> parts = str.Split(',');
        if (
            parts.Length != 2
            || !double.TryParse(parts[0], out double lon)
            || !double.TryParse(parts[1], out double lat)
        )
            return Zero;


        return new GeoPoint2d(lon, lat);
    }

    public int CompareTo(GeoPoint2d other)
    {
        int lonComparison = Lon.CompareTo(other.Lon);
        return lonComparison != 0 ? lonComparison : Lat.CompareTo(other.Lat);
    }
}