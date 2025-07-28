namespace Ellipse.Common.Models;

public enum Profile
{
    DrivingCar,

    DrivingHgv,

    FootWalking,

    CyclingRegular,
}

public static class ProfileExtensions
{
    public static string AsString(this Profile profile) =>
        profile switch
        {
            Profile.DrivingCar => "driving-car",
            Profile.DrivingHgv => "driving-hgv",
            Profile.FootWalking => "foot-walking",
            Profile.CyclingRegular => "cycling-regular",
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
        };
}
