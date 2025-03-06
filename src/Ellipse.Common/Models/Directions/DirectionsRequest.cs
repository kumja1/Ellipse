using Ellipse.Common.Enums;
using Ellipse.Common.Enums.Directions;


namespace Ellipse.Common.Models.Directions;

public class DirectionsRequest
{
    public RoutingProfile Profile { get; set; } = RoutingProfile.Driving;
    public List<GeoPoint2d> Waypoints { get; set; } = [];
    public bool Alternatives { get; set; } = false;

    public string AccessToken { get; set; } = string.Empty;
    
    public List<DirectionsAnnotationType>? Annotations { get; set; }
    public bool BannerInstructions { get; set; } = false;
    public bool? ContinueStraight { get; set; }
    public string? Exclude { get; set; }
    public GeometryType Geometries { get; set; } = GeometryType.Polyline;
    public string Language { get; set; } = "en";
    public OverviewType Overview { get; set; } = OverviewType.Simplified;
    public bool RoundaboutExits { get; set; } = false;
    public bool Steps { get; set; } = false;
    public bool VoiceInstructions { get; set; } = false;
    public VoiceUnitsType VoiceUnits { get; set; } = VoiceUnitsType.Imperial;
    public EngineType Engine { get; set; } = EngineType.ElectricNoRecharge;

    // EV-related parameters
    public double? EvInitialCharge { get; set; }
    public double? EvMaxCharge { get; set; }
    public List<EvConnectorType>? EvConnectorTypes { get; set; }
    public string? EnergyConsumptionCurve { get; set; }
    public string? EvChargingCurve { get; set; }
    public string? EvUnconditionedChargingCurve { get; set; }
    public double? EvPreConditioningTime { get; set; }
    public double? EvMaxAcChargingPower { get; set; }
    public double? EvMinChargeAtDestination { get; set; }
    public double? EvMinChargeAtChargingStation { get; set; }
    public double? AuxiliaryConsumption { get; set; }

    // Vehicle constraints
    public double? MaxHeight { get; set; } = 1.6;
    public double? MaxWidth { get; set; } = 1.9;
    public double? MaxWeight { get; set; } = 2.5;

    // Timing parameters
    public string? DepartAt { get; set; }
    public string? ArriveBy { get; set; }

    public NotificationsType Notifications { get; set; } = NotificationsType.All;
}

