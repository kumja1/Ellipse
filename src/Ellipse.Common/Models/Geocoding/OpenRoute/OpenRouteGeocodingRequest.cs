namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public record struct OpenRouteGeocodingRequest
{
    public OpenRouteGeocodingRequest() { }

    /// <summary>
    /// Search query (address or place name).
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Max number of results.
    /// </summary>
    public int Size { get; set; } = 10;
}
