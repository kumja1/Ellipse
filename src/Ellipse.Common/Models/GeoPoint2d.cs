namespace Ellipse.Common.Models;

public readonly record struct GeoPoint2d(double Lon, double Lat)
{
    public override string ToString() => $"{Lon},{Lat}";

    public static GeoPoint2d Zero => new(0d, 0d);

    public static bool TryParse(string str, out GeoPoint2d result)
    {
        result = Parse(str);
        return result != Zero;
    }

    public static GeoPoint2d Parse(string str)
    {
        Span<string> parts = str.Split(',');
        if (
            parts.Length != 2
            || !double.TryParse(parts[0], out double lon)
            || !double.TryParse(parts[1], out double lat)
        )
        {
            return Zero;
        }

        return new GeoPoint2d(lon, lat);
    }
}
