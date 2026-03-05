namespace Ellipse.Common.Models;

public readonly record struct BoundingBox
{
    public readonly LngLat Min;
    public readonly LngLat Max;

    public BoundingBox(List<LngLat> latLngs)
    {
        if (latLngs.Count == 0)
            throw new ArgumentException("LatLngs list cannot be empty", nameof(latLngs));

        double minLon = latLngs.Min(p => p.Lng);
        double maxLon = latLngs.Max(p => p.Lng);
        double minLat = latLngs.Min(p => p.Lat);
        double maxLat = latLngs.Max(p => p.Lat);

        Min = new LngLat(minLon, minLat);
        Max = new LngLat(maxLon, maxLat);
    }

    public BoundingBox(LngLat point, double radius)
    {
        double num1 = 360.0 * radius / 40075017.0;
        double num2 = num1 / Math.Cos(point.Lat * Math.PI / 180.0);

        Min = new LngLat(point.Lng - num2, point.Lat - num1);
        Max = new LngLat(point.Lng + num2, point.Lat + num1);
    }


    public IEnumerable<LngLat> GetPoints(double step)
    {
        if (step <= 0)
            yield break;

        for (double lat = Min.Lat; lat <= Max.Lat; lat += step)
        for (double lng = Min.Lng; lng <= Max.Lng; lng += step)
            yield return new LngLat(lng, lat);
    }
}