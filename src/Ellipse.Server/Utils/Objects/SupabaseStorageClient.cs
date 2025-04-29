using System.Text;
using Serilog;
using Supabase.Storage;
using Supabase.Storage.Interfaces;

namespace Ellipse.Server.Utils.Objects;

public sealed class SupabaseStorageClient(Supabase.Client client)
{
    private readonly Supabase.Client _client = client;
    private IStorageFileApi<FileObject>? _bucketApi;

    public async ValueTask InitBucket()
    {
        try
        {
            Bucket? bucket = await GetOrCreateBucket("server_cache");
            ArgumentNullException.ThrowIfNull(bucket);

            _bucketApi = _client.Storage.From(bucket.Id!);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured when initing the bucket");
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
            Log.Error(e, "An error occured while creating a bucket for {0}", name);
            throw;
        }
    }

    public async ValueTask Set(
        object obj,
        string content,
        string folder = "",
        Supabase.Storage.FileOptions options = null
    ) => await Set(obj.ToString(), content, folder, null);

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
        try
        {
            await _bucketApi?.Upload(data, $"{folder}/{key}.txt", options);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while uploading JSON string to {0}", key);
        }
    }

    public async ValueTask Remove(object obj) => await Remove(obj.ToString());

    public async ValueTask Remove(string name)
    {
        try
        {
            await _bucketApi?.Remove($"{name}.txt");
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while removing JSON {0}", name);
        }
    }

    public async Task<string> Get(object obj) => await Get(obj.ToString());

    public async Task<string> Get(string name)
    {
        try
        {
            byte[] data = await _bucketApi?.Download($"{name}.txt", null);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while fetching JSON {0}", name);
            return string.Empty;
        }
    }
}
