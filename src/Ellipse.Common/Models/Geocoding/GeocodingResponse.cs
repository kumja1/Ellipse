namespace Ellipse.Common.Models.Geocoding;

public class GeocodingResponse
{
    public string Type { get; set; }
    public List<string> Query { get; set; }
    public List<Feature> Features { get; set; }
    public string Attribution { get; set; }
}
