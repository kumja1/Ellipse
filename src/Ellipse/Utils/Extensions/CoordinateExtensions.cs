using OpenLayers.Blazor;

namespace Ellipse.Utils.Extensions;

public static class CoordinateExtensions
{
    
    public static bool Within(this Coordinate self, Coordinate other, double tolerance = 0.1) =>
        Math.Abs(self.Longitude - other.Longitude) <= tolerance && Math.Abs(self.Latitude - other.Latitude) <= tolerance;
}