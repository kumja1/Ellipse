namespace Ellipse.Server.Models;

public struct FileCacheOptions
{
    public float MaxSizePerFile { get; set; }

    public TimeSpan ExpirationScanInterval { get; set; }

    public string DirectoryName { get; set; }
}
