using Ellipse.Common.Models.Directions;

namespace Ellipse.Common.Models.Markers;

public sealed record MarkerResponse(
    string Address,
    double TotalDistance,
    Dictionary<string, Route> Routes
);
