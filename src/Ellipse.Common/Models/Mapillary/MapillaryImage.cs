using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Mapillary;

public class MapillaryImage
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("geometry")]
    public MapillaryGeometry Geometry { get; set; }

    [JsonPropertyName("thumb_256_url")]
    public string Thumb256Url { get; set; }

    [JsonPropertyName("thumb_1024_url")]
    public string Thumb1024Url { get; set; }

    [JsonPropertyName("captured_at")]
    public long CapturedAt { get; set; }

    [JsonPropertyName("compass_angle")]
    public double? CompassAngle { get; set; }

    [JsonPropertyName("creator_id")]
    public string CreatorId { get; set; }

    [JsonPropertyName("sequence_id")]
    public string SequenceId { get; set; }
}

public class MapillaryGeometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("coordinates")]
    public List<double> Coordinates { get; set; }
}
