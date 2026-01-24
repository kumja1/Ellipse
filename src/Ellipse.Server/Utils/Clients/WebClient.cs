using System.Globalization;
using System.Text;
using System.Text.Json;
using AngleSharp.Text;
using Serilog;

namespace Ellipse.Server.Utils.Clients;

public class WebClient(HttpClient client, string baseUrl, string apiKey = "") : IDisposable
{
    protected static void AppendParam<T>(
        StringBuilder builder,
        string key,
        T value,
        bool appendAnd = true
    )
    {
        if (string.IsNullOrEmpty(key) || Equals(value, default(T)))
            return;

        if (builder.Length == 0)
            builder.Append('?');

        if (appendAnd && builder.Length > 1)
            builder.Append('&');

        string paramValue = value switch
        {
            IEnumerable<string> enumerable => string.Join(',', enumerable),
            _ when value is bool or string or int or long or double or float => string.Format(
                CultureInfo.InvariantCulture, "{0}", value),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };

        builder.Append(key).Append('=').Append(Uri.EscapeDataString(paramValue));
    }

    private static void AppendParam(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        builder.Append(value);
    }

    private static void AppendPath(StringBuilder builder, string path, bool appendSlash = true)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (appendSlash)
        {
            builder.Append('/');
            path = path.TrimStart('/');
        }

        builder.Append(path);
    }

    private async Task<T> SendRequest<T>(HttpRequestMessage? request = null)
    {
        request ??= new HttpRequestMessage(HttpMethod.Get, baseUrl);
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Add("Authorization", apiKey);

        Log.Information("Request: {Request}", request);
        Log.Information("Request URL: {Url}", request.RequestUri);

        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
               ?? throw new JsonException("'response.Content' could not be parsed");
    }

    protected async Task<TResponse> GetRequest<TResponse>(
        string parameters = "",
        params string[] paths
    ) =>
        await SendRequest<TResponse>(
            new HttpRequestMessage(HttpMethod.Get, BuildUrl(paths, parameters))
        );

    protected async Task<TResponse> PostRequestAsync<TRequest, TResponse>(
        TRequest request,
        params string[] paths
    )
    {
        string url = BuildUrl(paths);
        Log.Information("Request URL: {Url}", url);
        string json = JsonSerializer.Serialize(request);
        Log.Information("Request JSON: {Json}", json);
        StringContent content = new(json, Encoding.UTF8, "application/json");
        try
        {
            return await SendRequest<TResponse>(
                new HttpRequestMessage(HttpMethod.Post, url) { Content = content }
            );
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while sending the request {1}, URl: {0}", url, request);
            throw;
        }
    }

    protected string BuildQueryParams<TRequest>(
        TRequest request,
        Action<TRequest, StringBuilder> buildParams
    )
    {
        StringBuilder builder = StringBuilderPool.Obtain();
        buildParams(request, builder);
        
        return builder.ToPool(); 
    }

    private string BuildUrl(string[] paths, string parameters = "")
    {
        StringBuilder builder = StringBuilderPool.Obtain();
        AppendPath(builder, baseUrl, false);

        if (paths.Length > 0)
            AppendPath(builder, string.Join("/", paths));

        if (!string.IsNullOrEmpty(parameters))
            AppendParam(builder, parameters.TrimEnd('&'));

        return builder.ToString();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        client.Dispose();
    }
}