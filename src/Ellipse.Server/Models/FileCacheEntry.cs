namespace Ellipse.Server.Models;

public class FileCacheEntry
{
    public object Key { get; set; }

    public object Value { get; set; }

    public TimeSpan ExpiresAfter { get; set; }
}
