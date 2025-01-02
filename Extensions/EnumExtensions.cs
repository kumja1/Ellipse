
using Ellipse.Enums;

namespace Ellipse.Extensions;

public static class EnumExtensions
{
    public static string ToStringValue(this RoutingProfile profile)
    {
        return profile switch
        {
            RoutingProfile.DrivingWithTraffic => "driving-traffic",
            RoutingProfile.Driving => "driving",
            RoutingProfile.Walking => "walking",
            RoutingProfile.Cycling => "cycling",
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
    }

    public static string ToStringValue(this MatrixAnnotationType annotation)
    {
        return annotation switch
        {
            MatrixAnnotationType.Duration => "duration",
            MatrixAnnotationType.Distance => "distance",
            MatrixAnnotationType.Both => "duration,distance",
            _ => throw new ArgumentOutOfRangeException(nameof(annotation), annotation, null)
        };
    }


    public static string ToStringValue(this EngineType engine)
    {
        return engine switch
        {
            EngineType.ElectricNoRecharge => "electric_no_recharge",
            EngineType.Electric => "electric",
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null)
        };
    }

    public static string ToStringValue(this EvConnectorType connector)
    {
        return connector switch
        {
            EvConnectorType.CcsComboType1 => "ccs_combo_type1",
            EvConnectorType.Tesla => "tesla",
            _ => throw new ArgumentOutOfRangeException(nameof(connector), connector, null)
        };
    }
}
