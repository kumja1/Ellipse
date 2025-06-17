using System.Text;
using Serilog;
using Supabase.Storage;
using Supabase.Storage.Interfaces;

namespace Ellipse.Server.Utils.Objects.Clients;

public sealed class SupabaseStorageClient(Supabase.Client client)
{
    private readonly Supabase.Client _client = client;
    private IStorageFileApi<FileObject>? _bucketApi;

    public async ValueTask InitializeAsync()
    {
        try
        {
            Bucket? bucket = await GetOrCreateBucket("server_cache");
            ArgumentNullException.ThrowIfNull(bucket);

            _bucketApi = _client.Storage.From(bucket.Id!);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred when initializing the bucket");
        }
    }

    private async Task<Bucket?> GetOrCreateBucket(string name)
    {
        try
        {
            Bucket? bucket = await _client.Storage.GetBucket(name);
            if (bucket != null)
                return bucket;

            await _client.Storage.CreateBucket(
                name,
                new BucketUpsertOptions { FileSizeLimit = "30MB", AllowedMimes = ["text/plain"] }
            );

            return await _client.Storage.GetBucket(name);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while creating a bucket for {0}", name);
            throw;
        }
    }

    public async ValueTask Set(
        object obj,
        string content,
        string folder = "",
        Supabase.Storage.FileOptions? options = null
    ) => await Set(obj.ToString(), content, folder, options);

    public async ValueTask Set(
        string key,
        string content,
        string folder = "",
        Supabase.Storage.FileOptions? options = null
    )
    {
        options ??= new Supabase.Storage.FileOptions
        {
            ContentType = "text/plain",
            Upsert = true,
            CacheControl = "3600",
        };

        byte[] data = Encoding.UTF8.GetBytes(content);
        string path = BuildPath(key, folder);

        try
        {
            await _bucketApi?.Upload(data, path, options);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while uploading data to {0}", path);
        }
    }

    public async ValueTask Remove(object obj, string folder = "") =>
        await Remove(obj.ToString(), folder);

    public async ValueTask Remove(string key, string folder = "")
    {
        string path = BuildPath(key, folder);

        try
        {
            await _bucketApi?.Remove(path);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while removing data for {0}", path);
        }
    }

    public async Task<string> Get(object obj, string folder = "") =>
        await Get(obj.ToString(), folder);

    public async Task<string> Get(string key, string folder = "")
    {
        string path = BuildPath(key, folder);

        try
        {
            byte[] data = await _bucketApi?.Download(path, null);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while fetching data for {0}", path);
            return string.Empty;
        }
    }

    private static string BuildPath(string key, string folder) =>
        string.IsNullOrWhiteSpace(folder) ? $"{key}.txt" : $"{folder.TrimEnd('/')}/{key}.txt";
}
