using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Matrix.OpenRoute;

// Request Model
public class OpenRouteMatrixRequest
{
    [JsonPropertyName("locations")]
    public double[][] Locations { get; set; } = [];

    [JsonPropertyName("sources")]
    public List<int>? Sources { get; set; }

    [JsonPropertyName("destinations")]
    public List<int>? Destinations { get; set; }

    [JsonPropertyName("metrics")]
    public List<string>? Metrics { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }

    [JsonPropertyName("profile")]
    public Profile Profile { get; set; } = Profile.DrivingCar;

    [JsonPropertyName("resolve_locations")]
    public bool? ResolveLocations { get; set; }

    [JsonPropertyName("optimized")]
    public bool? Optimized { get; set; }

    [JsonPropertyName("dry_run")]
    public bool? DryRun { get; set; }
}
