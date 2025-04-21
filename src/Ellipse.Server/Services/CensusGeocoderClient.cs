using System.Globalization;
using System.Text;
using System.Text.Json;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models.Geocoding;

namespace Ellipse.Server.Services;

public sealed class CensusGeocoderClient(HttpClient client) : IDisposable
{
    private const string BaseUrl = "https://geocoding.geo.census.gov/geocoder/";

    public async Task<GeocodingResponse> Geocode(GeocodingRequest request)
    {
        var returnType = request.ReturnType.ToString().ToLowerInvariant();
        var searchType = request.SearchType switch
        {
            SearchType.OnelineAddress => "onelineaddress",
            SearchType.Address => "address",
            SearchType.AddressPR => "addressPR",
            _ => throw new ArgumentOutOfRangeException(
                $"Search type {request.SearchType} is not valid for this request."
            ),
        };

        var url = $"{BaseUrl}{returnType}/{searchType}?{BuildQueryParams(request)}";
        return await GetResponse(url);
    }

    public async Task<GeocodingResponse> ReverseGeocode(ReverseGeocodingRequest request)
    {
        if (request.ReturnType != ReturnType.Geographies)
            throw new ArgumentOutOfRangeException(
                $"Return type {request.ReturnType} must be ReturnType.Geographies for this request"
            );

        if (request.SearchType != SearchType.Coordinates)
            throw new ArgumentOutOfRangeException(
                $"Search type {request.SearchType} must be SearchType.Coordinates for this request"
            );

        var url =
            $"{BaseUrl}{request.ReturnType.ToString().ToLowerInvariant()}/{request.SearchType.ToString().ToLowerInvariant()}?{BuildQueryParams(request)}";
        return await GetResponse(url);
    }

    public async Task<GeocodingResponse> GetResponse(string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GeocodingResponse>()
            ?? throw new JsonException("'response.Content' could not be parsed");
    }

    public static string BuildQueryParams(GeocodingRequest request)
    {
        StringBuilder builder = new();
        return request switch
        {
            GeocodingRequest geocodeRequest => BuildGeocodeQueryParams(geocodeRequest, builder),
            _ when request is ReverseGeocodingRequest reverseGeocodeRequest =>
                BuildReverseGeocodeQueryParams(reverseGeocodeRequest, builder),
            _ => throw new ArgumentException("Unknown request type", nameof(request)),
        };
    }

    private static string BuildGeocodeQueryParams(GeocodingRequest request, StringBuilder builder)
    {
        AppendParam(builder, "benchmark", request.Benchmark);
        AppendParam(builder, "vintage", request.Vintage);
        AppendParam(builder, "format", request.Format.ToString().ToLowerInvariant());

        AppendParam(builder, "callback", request.Callback);
        AppendParam(builder, "layers", request.Layers);

        switch (request.SearchType)
        {
            case SearchType.OnelineAddress:
                AppendParam(builder, "address", request.Address);
                break;
            case SearchType.Address:
                AppendParam(builder, "street", request.Street);
                AppendParam(builder, "city", request.City);
                AppendParam(builder, "state", request.State);
                AppendParam(builder, "zip", request.Zip);
                break;
            case SearchType.AddressPR:
                AppendParam(builder, "street", request.Street);
                AppendParam(builder, "urb", request.Urb);
                AppendParam(builder, "city", request.City);
                AppendParam(builder, "municipio", request.Municipio);
                AppendParam(builder, "state", request.State);
                AppendParam(builder, "zip", request.Zip);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    $"Search type {request.SearchType} is not valid for this request"
                );
        }

        return builder.ToString().TrimEnd('&');
    }

    private static string BuildReverseGeocodeQueryParams(
        ReverseGeocodingRequest request,
        StringBuilder builder
    )
    {
        AppendParam(builder, "returntype", request.ReturnType.ToString().ToLowerInvariant());
        AppendParam(builder, "searchtype", "coordinates");

        AppendParam(builder, "benchmark", request.Benchmark);
        AppendParam(builder, "vintage", request.Vintage);

        AppendParam(builder, "format", request.Format.ToString().ToLowerInvariant());
        AppendParam(builder, "callback", request.Callback);
        AppendParam(builder, "layers", request.Layers);

        if (request.X.HasValue && request.Y.HasValue)
        {
            AppendParam(builder, "x", request.X.Value.ToString(CultureInfo.InvariantCulture));
            AppendParam(builder, "y", request.Y.Value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            throw new ArgumentException(
                "Reverse geocoding requests require both X and Y coordinates."
            );
        }

        return builder.ToString().TrimEnd('&');
    }

    private static void AppendParam(StringBuilder builder, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            builder.Append($"{key}={Uri.EscapeDataString(value)}&"); // Use Uri.EscapeDataString to make the parameter URL-safe.
    }

    public void Dispose() => client.Dispose();
}
