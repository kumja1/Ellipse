namespace Ellipse.Common.Models;

public readonly record struct LngLat(double Lng, double Lat) : IComparable<LngLat>
{
    public override string ToString() => $"{Lng},{Lat}";

    public static LngLat Zero => new(0d, 0d);

    public static LngLat Parse(string str)
    {
        Span<string> parts = str.Split(',');
        if (
            parts.Length != 2
            || !double.TryParse(parts[0], out double lon)
            || !double.TryParse(parts[1], out double lat)
        )
            return Zero;


        return new LngLat(lon, lat);
    }

    public int CompareTo(LngLat other)
    {
        int lonComparison = Lng.CompareTo(other.Lng);
        return lonComparison != 0 ? lonComparison : Lat.CompareTo(other.Lat);
    }
}