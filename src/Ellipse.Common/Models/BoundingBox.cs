namespace Ellipse.Common.Models;

public readonly record struct BoundingBox
{
    public double MinLat { get; }
    public double MaxLat { get; }
    public double MinLng { get; }
    public double MaxLng { get; }

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
        MinLat = point.Lat - radius;
        MaxLat = point.Lat + radius;
        MinLng = point.Lon - radius;
        MaxLng = point.Lon + radius;
    }

    // Gets all points within the bounding box at the specified step size
    public IEnumerable<GeoPoint2d> GetPoints(double step)
    {
        for (double lat = Math.Floor(MinLat / step) * step; lat <= MaxLat; lat += step)
        for (double lon = Math.Floor(MinLng / step) * step; lon <= MaxLng; lon += step)
            yield return new GeoPoint2d(lon, lat);
    }
}