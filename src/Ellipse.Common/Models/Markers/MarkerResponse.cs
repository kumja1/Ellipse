using Ellipse.Common.Models.Directions;

namespace Ellipse.Common.Models.Markers;

public sealed record MarkerResponse(string PointName, Dictionary<string, Route> Distances);