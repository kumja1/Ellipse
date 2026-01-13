using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Mapillary;

public class MapillaryResponse<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = [];

    [JsonPropertyName("paging")]
    public MapillaryPaging Paging { get; set; }
}

public class MapillaryPaging
{
    [JsonPropertyName("cursors")]
    public MapillaryCursors Cursors { get; set; }

    [JsonPropertyName("next")]
    public string Next { get; set; }
}

public class MapillaryCursors
{
    [JsonPropertyName("before")]
    public string Before { get; set; }

    [JsonPropertyName("after")]
    public string After { get; set; }
}
