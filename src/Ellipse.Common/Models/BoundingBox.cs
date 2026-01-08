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
        
        const double earthRadius = 6371000.0;
        const double degToRad = Math.PI / 180.0;

        double dLng = MaxLng - MinLng;
        if (dLng < 0) dLng += 360;
        
        Area = Math.Pow(earthRadius, 2) * 
               Math.Abs(Math.Sin(MaxLat * degToRad) - Math.Sin(MinLat * degToRad)) * 
               (dLng * degToRad);
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
        for (double lat = Math.Floor(MinLat / step) * step; lat <= MaxLat; lat += step)
        for (double lon = Math.Floor(MinLng / step) * step; lon <= MaxLng; lon += step)
            yield return new GeoPoint2d(lon, lat);
    }
}