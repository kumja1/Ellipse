namespace Ellipse.Common.Models.Geocoding.PhotonGeocoder;

public class PhotonGeocodingRequest
{
    public required string Query { get; set; }
    public int Limit { get; set; } = 10;
    public required string Lang { get; set; }
    public required string[] Layers { get; set; }
}
