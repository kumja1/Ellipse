namespace Ellipse.Common.Models;

public readonly record struct BoundingBox
{
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLng { get; }
    public double MaxLng { get; }

    public double Area { get; init; }
 
    public BoundingBox(IEnumerable<GeoPoint2d> latLngs)
    {
        if (!latLngs.Any())
            throw new ArgumentException("LatLngs list cannot be empty", nameof(latLngs));

        MinLat = latLngs.Min(latLng => latLng.Lat);
        MaxLat = latLngs.Max(latLng => latLng.Lat);
        MinLng = latLngs.Min(latLng => latLng.Lon);
        MaxLng = latLngs.Max(latLng => latLng.Lon);
    }

    public BoundingBox(GeoPoint2d point, double radius)
    {
        double num1 = 360.0 * radius / 40075017.0;
        double num2 = num1 / Math.Cos(point.Lat * Math.PI / 180.0);
        MinLat = point.Lat - num2;
        MaxLat = point.Lat + num2;
        MinLng = point.Lon - num2;
        MaxLng = point.Lon + num2;
    }

    // Gets all points within the bounding box at the specified step size
    public IEnumerable<GeoPoint2d> GetPoints(double step)
    {
        if (step <= 0)
        {
            yield break;
        }

        for (double lat = MinLat; lat <= MaxLat; lat += step)
        for (double lon = MinLng; lon <= MaxLng; lon += step)
            yield return new GeoPoint2d(lon, lat);
    }
}