namespace Ellipse.Common.Models.Geocoding.OpenRoute;

public record struct OpenRouteReverseGeocodingRequest
{
    public OpenRouteReverseGeocodingRequest() { }

    /// <summary>
    /// Longitude of the location.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Latitude of the location.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Max number of results.
    /// </summary>
    public int Size { get; set; } = 1;
}
