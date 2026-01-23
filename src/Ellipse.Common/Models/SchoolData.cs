using System.Text.Json.Serialization;

namespace Ellipse.Common.Models;

public readonly record struct SchoolData(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("PrincipalName")] string PrincipalName,
    [property: JsonPropertyName("PhoneNumber")] string PhoneNumber,
    [property: JsonPropertyName("Division")] string Division,
    [property: JsonPropertyName("GradeSpan")] string GradeSpan,
    [property: JsonPropertyName("SchoolType")] string SchoolType,
    [property: JsonPropertyName("Address")] string Address,
    [property: JsonPropertyName("LatLng")] GeoPoint2d LatLng
);
