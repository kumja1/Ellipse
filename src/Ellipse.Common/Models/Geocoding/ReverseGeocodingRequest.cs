using Ellipse.Common.Enums.Geocoding;

namespace Ellipse.Common.Models.Geocoding;

/// <summary>
/// A specialized reverse geocoding request that ensures the correct search type is set.
/// </summary>
public class ReverseGeocodingRequest : GeocodingRequest
{
    public ReverseGeocodingRequest()
    {
        // For reverse geocoding, only the Coordinates search type is valid.
        SearchType = SearchType.Coordinates;
        // The API's reverse operation requires geographic lookup.
        ReturnType = ReturnType.Geographies;
    }
}
