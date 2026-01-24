namespace Ellipse.Common.Models.Markers;

public sealed record BatchMarkerRequest(GeoPoint2d[] Points, SchoolData[] Schools);
