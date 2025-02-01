using Ellipse.Enums;

namespace Ellipse.Models.Geocoding;

public class Geometry
{
    public GeocodingGeometryType Type { get; set; }
    public GeoPoint2d Coordinates { get; set; }
    public bool? Interpolated { get; set; }
    public bool? Omitted { get; set; }
}
