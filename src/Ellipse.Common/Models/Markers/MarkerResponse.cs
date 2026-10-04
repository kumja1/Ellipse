using Ellipse.Common.Models.Directions;

namespace Ellipse.Common.Models.Markers;

public sealed record MarkerResponse(
    string Address,
    LngLat LngLat,
    // string Image256Url,
    // string Image1024Url,
    Dictionary<string, SchoolRoute> Routes
);