namespace Ellipse.Common.Models;

public readonly record struct SchoolData(
    string Name,
    string Division,
    string GradeSpan,
    string Address,
    GeoPoint2d LatLng
);
