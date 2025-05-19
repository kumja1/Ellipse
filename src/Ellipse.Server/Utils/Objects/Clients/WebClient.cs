using System.Text;
using System.Text.Json;
using AngleSharp.Text;
using Serilog;

namespace Ellipse.Server.Utils.Objects.Clients;

public abstract class WebClient(HttpClient client, string baseUrl, string apiKey = "") : IDisposable
{
    protected static void AppendParam(
        StringBuilder builder,
        string key,
        string value,
        bool appendAnd = true
    )
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (builder.Length == 0)
            builder.Append('?');

        if (string.IsNullOrEmpty(value))
            value = string.Empty;

        if (appendAnd && builder.Length > 1)
            builder.Append('&');

        builder.Append(key).Append('=').Append(Uri.EscapeDataString(value));
    }

    protected static void AppendParam(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        builder.Append(value);
    }

    protected static void AppendPath(StringBuilder builder, string path, bool appendSlash = true)
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

    protected async Task<T> SendRequestAsync<T>(HttpRequestMessage? request = null)
    {
        request ??= new HttpRequestMessage(HttpMethod.Get, baseUrl);
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Add("Authorization", apiKey);

        Log.Information("Request: {Request}", request);
        Log.Information("Request URL: {Url}", request.RequestUri);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new JsonException("'response.Content' could not be parsed");
    }

    protected async Task<TResponse> GetRequestAsync<TRequest, TResponse>(
        string parameters = "",
        params string[] additionalArgs
    )
    {
        var url = BuildUrl(additionalArgs, parameters);
        return await SendRequestAsync<TResponse>(new HttpRequestMessage(HttpMethod.Get, url));
    }

    protected async Task<TResponse> PostRequestAsync<TRequest, TResponse>(
        TRequest request,
        params string[] additionalArgs
    )
    {
        var url = BuildUrl(additionalArgs);
        Log.Information("Request URL: {Url}", url);
        var json = JsonSerializer.Serialize(request);
        Log.Information("Request JSON: {Json}", json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            return await SendRequestAsync<TResponse>(
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
        Func<TRequest, StringBuilder, string> buildParams
    )
    {
        StringBuilder builder = StringBuilderPool.Obtain();
        return buildParams(request, builder);
    }

    protected string BuildUrl(string[] additionalArgs, string parameters = "")
    {
        StringBuilder builder = StringBuilderPool.Obtain();
        AppendPath(builder, baseUrl, false);

        if (additionalArgs.Length > 0)
            AppendPath(builder, string.Join("/", additionalArgs), true);

        if (parameters != null)
            AppendParam(builder, parameters);

        return builder.ToString();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        client?.Dispose();
    }
}
