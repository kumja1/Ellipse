using Ellipse.Common.Models.Directions;

namespace Ellipse.Common.Models.Markers;

public sealed record MarkerResponse(
    string Address,
    // string Image256Url,
    // string Image1024Url,
    double TotalDistance,
    Dictionary<string, Route> Routes
);
