using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ellipse.Server.Models;

namespace Ellipse.Server.Utils
{
    public sealed class FileCache
    {
        private static readonly FileCacheOptions DefaultOptions = new()
        {
            DirectoryName = "server-cache",
            ExpirationScanInterval = TimeSpan.FromMinutes(5),
            MaxSizePerFile = 30f,
        };

        private readonly string _cacheDir;
        private readonly string _metadataPath;
        private readonly TimeSpan _scanInterval;
        private readonly long _maxBytesPerFile;

        private FileStream _currentStream;
        private StreamWriter _currentWriter;
        private int _currentIndex;

        private readonly ConcurrentDictionary<string, FileCacheEntryOptions> _entryMap;
        private readonly object _writeLock = new();

        public FileCache()
            : this(DefaultOptions) { }

        public FileCache(FileCacheOptions options)
        {
            _cacheDir = Path.Combine(Environment.CurrentDirectory, options.DirectoryName);
            Directory.CreateDirectory(_cacheDir);

            _metadataPath = Path.Combine(_cacheDir, "metadata.json");
            _scanInterval = options.ExpirationScanInterval;
            _maxBytesPerFile = (long)(options.MaxSizePerFile * 1024 * 1024);

            _entryMap = LoadMetadata();

            _currentIndex = DetermineCurrentIndex();
            OpenCurrentStream();
            StartSweeper();
        }

        public bool TryGet<T>(object keyObj, out T? value)
        {
            if (keyObj == null)
                throw new ArgumentNullException(nameof(keyObj));

            var key = keyObj.ToString()!;
            if (!_entryMap.TryGetValue(key, out var entry))
            {
                value = default;
                return false;
            }

            var filePath = Path.Combine(_cacheDir, entry.FileName);
            using var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );
            fs.Seek(entry.Offset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8, leaveOpen: false);
            var line = reader.ReadLine();
            if (line == null)
            {
                value = default;
                return false;
            }
            using var doc = JsonDocument.Parse(line);
            var elem = doc.RootElement.GetProperty("value");
            value = elem.Deserialize<T>();
            return true;
        }

        public void Set<T>(object keyObj, T value, FileCacheEntryOptions options)
            where T : notnull
        {
            if (keyObj == null)
                throw new ArgumentNullException(nameof(keyObj));
            var key = keyObj.ToString()!;
            options.TimeStamp = DateTime.UtcNow;

            lock (_writeLock)
            {
                if (!_entryMap.ContainsKey(key) && _currentStream.Length >= _maxBytesPerFile)
                {
                    _currentWriter.Dispose();
                    _currentStream.Dispose();
                    _currentIndex++;
                    OpenCurrentStream();
                }

                var offset = _currentStream.Position;
                var entryObj = new { key, value };
                var line = JsonSerializer.Serialize(entryObj);
                _currentWriter.WriteLine(line);
                _currentWriter.Flush();

                options.FileName = $"cache_{_currentIndex:D2}.jsonl";
                options.Offset = offset;
                _entryMap[key] = options;

                SaveMetadata();
            }
        }

        private void OpenCurrentStream()
        {
            var fileName = $"cache_{_currentIndex:D2}.jsonl";
            var path = Path.Combine(_cacheDir, fileName);
            _currentStream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read
            );
            _currentWriter = new StreamWriter(_currentStream, Encoding.UTF8) { AutoFlush = true };
        }

        private int DetermineCurrentIndex()
        {
            var files = Directory.GetFiles(_cacheDir, "cache_*.jsonl");
            int max = -1;
            foreach (var f in files)
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (name.StartsWith("cache_") && int.TryParse(name["cache_".Length..], out var idx))
                    max = Math.Max(max, idx);
            }
            return max < 0 ? 0 : max;
        }

        private void StartSweeper()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    var now = DateTime.UtcNow;
                    var removed = false;
                    foreach (var kvp in _entryMap)
                    {
                        if (now - kvp.Value.TimeStamp >= kvp.Value.AbsoluteExpirationRelativeToNow)
                        {
                            _entryMap.TryRemove(kvp.Key, out _);
                            removed = true;
                        }
                    }
                    if (removed)
                        SaveMetadata();
                    await Task.Delay(_scanInterval);
                }
            });
        }

        private ConcurrentDictionary<string, FileCacheEntryOptions> LoadMetadata()
        {
            if (!File.Exists(_metadataPath))
                return new ConcurrentDictionary<string, FileCacheEntryOptions>();

            using var stream = File.OpenRead(_metadataPath);
            return JsonSerializer.Deserialize<ConcurrentDictionary<string, FileCacheEntryOptions>>(
                    stream
                ) ?? new ConcurrentDictionary<string, FileCacheEntryOptions>();
        }

        private void SaveMetadata()
        {
            using var fs = new FileStream(
                _metadataPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            JsonSerializer.Serialize(fs, _entryMap);
        }
    }
}
