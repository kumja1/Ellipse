namespace Ellipse.Common.Models.Geocoding.PhotonGeocoder;

public class PhotonGeocodingRequest
{
    public string Query { get; set; }
    public int Limit { get; set; } = 10;
    public string Lang { get; set; }
    public string[] Layers { get; set; }
}
