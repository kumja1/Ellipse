namespace Ellipse.Common.Models.Mapillary;

public class MapillarySearchRequest
{
    public double? MinLon { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLon { get; set; }
    public double? MaxLat { get; set; }
    public int? Limit { get; set; }
    public string Fields { get; set; } = "id,geometry,thumb_256_url,thumb_1024_url,captured_at,compass_angle,creator_id,sequence_id";
}
