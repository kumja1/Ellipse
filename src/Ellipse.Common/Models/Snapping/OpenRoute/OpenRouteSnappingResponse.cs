using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Snapping.OpenRoute;

public class OpenRouteSnappingResponse
{
    [JsonPropertyName("locations")]
    public List<SnappedLocation?> Locations { get; set; } = [];
}
