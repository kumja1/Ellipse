using Ellipse.Common.Enums.Geocoding;

namespace Ellipse.Common.Models.Geocoding;

public class Geometry
{
    public GeocodingGeometryType Type { get; set; }
    public GeoPoint2d Coordinates { get; set; }
    public bool? Interpolated { get; set; }
    public bool? Omitted { get; set; }
}
