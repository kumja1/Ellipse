namespace Ellipse.Server.Models;

public struct FileCacheEntryOptions
{
    public TimeSpan AbsoluteExpirationRelativeToNow { get; set; }

    public DateTime TimeStamp { get; set; }

    public long Offset { get; set; }

    public string FileName { get; set; }
}
