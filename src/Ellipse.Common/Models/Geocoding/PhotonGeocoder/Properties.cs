namespace Ellipse.Common.Models.Geocoding.PhotonGeocoder;

public class Properties
{
    public required string OsmType { get; set; }
    public long OsmId { get; set; }
    public required string Name { get; set; }
    public required string Country { get; set; }
    public required string City { get; set; }
    public required string Street { get; set; }
    public required string Postcode { get; set; }

    public required string State { get; set; }
}
