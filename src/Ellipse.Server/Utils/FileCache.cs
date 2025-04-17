using Ellipse.Server.Models;
using LiteDB;

namespace Ellipse.Server.Utils;

public sealed class FileCache : IDisposable
{
    private readonly ILiteCollection<FileCacheEntry> _col;

    private readonly LiteDatabase _db;

    public FileCache(string fileName)
    {
        _db = new(Path.Join(Environment.CurrentDirectory, fileName, ".db"));
        _col = _db.GetCollection<FileCacheEntry>("server_cache");
        _col.EnsureIndex(x => x.Key, unique: true);
    }

    public bool TryGet<T>(object key, out T? value)
    {
        var entry = _col.FindOne(x => x.Key == key);
        if (entry == null)
        {
            value = default;
            return false;
        }

        value = (T?)entry.Value;
        return value != null;
    }

    public void Set<T>(object key, T value)
        where T : notnull => _col.Upsert(new FileCacheEntry { Key = key, Value = value });

    public void Dispose()
    {
        _db.Rebuild();
        _db.Dispose();
    }
}
