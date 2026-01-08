using Community.Blazor.MapLibre;
using Community.Blazor.MapLibre.Models;
using Community.Blazor.MapLibre.Models.Marker;

namespace Ellipse.Utils.Extensions;

public static class MapLibreExtensions
{
    extension(LngLat self)
    {
        public bool Within(LngLat other, double tolerance = 0.1) =>
            Math.Abs(self.Longitude - other.Longitude) <= tolerance && Math.Abs(self.Latitude - other.Latitude) <= tolerance;
    }

    extension(MapLibre self)
    {
        public async Task UpdateMarker(MarkerOptions options, LngLat position, Guid id)
        {
            await Task.WhenAll(self.RemoveMarker(id),self.AddMarker(options, position, id));
        }
    }
}