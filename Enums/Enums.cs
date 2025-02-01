namespace Ellipse.Enums;

public enum MatrixAnnotationType
{
    Duration,
    Distance,
   Both,

}

public enum DirectionsAnnotationType
{
    Duration,
    Distance,
    Speed,
    Congestion
}


public enum GeometryType
{
    GeoJson,
    Polyline,
    Polyline6
}

public enum OverviewType
{
    Simplified,
    Full,
    False
}

public enum VoiceUnitsType
{
    Imperial,
    Metric
}

public enum EngineType
{
    ElectricNoRecharge,
    Electric
}

public enum EvConnectorType
{
    CcsComboType1,
    Tesla
}

public enum NotificationsType
{
    All,
    None
}


public enum RoutingProfile
{
    Driving,
    Walking,
    Cycling,
    DrivingWithTraffic,
}


public enum Approach
{
    Unrestricted,
    Curb
}


public enum PlaceType
{
    Country,
    Region,
    Postcode,
    District,
    Place,
    Locality,
    Neighborhood,
    Address
}

public enum Accuracy
{
    Rooftop,
    Parcel,
    Point,
    Interpolated,
    Intersection,
    Approximate,
    Street
}

public enum GeocodingGeometryType
{
    Point
}