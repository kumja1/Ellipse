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

    /// <summary>
    /// Layers to be queried (e.g., venue, address, street, neighbourhood, locality, county, region, country, coarse).
    /// </summary>
    public string[]? Layers { get; set; }

    /// <summary>
    /// Sources to be queried (e.g., openstreetmap, openaddresses, whosonfirst, geonames).
    /// </summary>
    public string[]? Sources { get; set; }

    /// <summary>
    /// Boundary circle radius in kilometers for filtering results around the point.
    /// </summary>
    public double? BoundaryCircleRadius { get; set; }

    /// <summary>
    /// Boundary country code(s) to filter results (ISO 3166-1 alpha-2 or alpha-3).
    /// </summary>
    public string[]? BoundaryCountry { get; set; }
}
