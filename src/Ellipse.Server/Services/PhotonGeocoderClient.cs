using System.Globalization;
using System.Text;
using System.Text.Json;
using Ellipse.Common.Models.Geocoding.CensusGeocoder.PhotonGeocoder;


namespace Ellipse.Server.Services;

public sealed class PhotonGeocoderClient : IDisposable
{
    private const string BaseUrl = "https://photon.komoot.io/";
    private readonly HttpClient _client;

    public PhotonGeocoderClient(HttpClient client)
    {
        _client = client;
        _client.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<PhotonGeocodeResponse> GeocodeAsync(PhotonGeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ArgumentException("Query must be provided.", nameof(request.Query));

        var url = $"api/?{BuildQueryParams(request)}";
        return await SendRequestAsync<PhotonGeocodeResponse>(url);
    }

    public async Task<PhotonGeocodeResponse> ReverseGeocodeAsync(PhotonReverseGeocodeRequest request)
    {
        var url = $"reverse?{BuildQueryParams(request)}";
        return await SendRequestAsync<PhotonGeocodeResponse>(url);
    }

    private async Task<T> SendRequestAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream)
               ?? throw new JsonException("Unable to deserialize response");
    }

    private string BuildQueryParams(object request)
    {
        StringBuilder builder = new();
        return request switch
        {
            PhotonGeocodeRequest geocodeRequest => BuildGeocodeParams(builder, geocodeRequest),
            PhotonReverseGeocodeRequest reverseGeocodeRequest => BuildReverseGeocodeQueryParams(builder, reverseGeocodeRequest),
            _ => throw new NotSupportedException($"Request {request} is not supported for query parameters.")
        };
    }

    private string BuildGeocodeParams(StringBuilder builder, PhotonGeocodeRequest request)
    {
        AppendParam(builder, "q", request.Query);
        AppendParam(builder, "limit", request.Limit.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "lang", request.Lang);
        if (request != null && request.Layers?.Length > 0)
            AppendParam(builder, "osm_tag", string.Join(',', request.Layers));

        return builder.ToString().TrimEnd('&');
    }

    private string BuildReverseGeocodeQueryParams(StringBuilder builder, PhotonReverseGeocodeRequest request)
    {
        AppendParam(builder, "lat", request.Latitude.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "lon", request.Longitude.ToString(CultureInfo.InvariantCulture));
        return builder.ToString().TrimEnd('&');
    }


    private static void AppendParam(StringBuilder builder, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append(key)
                   .Append('=')
                   .Append(Uri.EscapeDataString(value))
                   .Append('&');
        }
    }

    public void Dispose() => _client.Dispose();
}
