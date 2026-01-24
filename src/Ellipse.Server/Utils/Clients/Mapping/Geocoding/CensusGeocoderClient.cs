using System.Globalization;
using System.Text;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;

namespace Ellipse.Server.Utils.Clients.Mapping.Geocoding;

public sealed class CensusGeocoderClient(HttpClient client)
    : WebClient(client, "https://geocoding.geo.census.gov/geocoder/"),
        IGeocoderClient<
            CensusGeocodingRequest,
            CensusReverseGeocodingRequest,
            CensusGeocodingResponse
        >
{
    public async Task<CensusGeocodingResponse> Geocode(CensusGeocodingRequest request)
    {
        string returnType = request.ReturnType.ToString().ToLowerInvariant();
        string searchType = request.SearchType switch
        {
            SearchType.OnelineAddress => "onelineaddress",
            SearchType.Address => "address",
            SearchType.AddressPR => "addressPR",
            _ => throw new ArgumentOutOfRangeException(
                $"Search type {request.SearchType} is not valid for this request."
            ),
        };

        return await GetRequest<CensusGeocodingResponse>(
            BuildQueryParams(request, BuildGeocodeQueryParams),
            $"{returnType}/{searchType}"
        );
    }

    public async Task<CensusGeocodingResponse> ReverseGeocode(CensusReverseGeocodingRequest request)
    {
        if (request.ReturnType != ReturnType.Geographies)
            throw new ArgumentOutOfRangeException(
                $"Return type {request.ReturnType} must be ReturnType.Geographies for this request"
            );

        if (request.SearchType != SearchType.Coordinates)
            throw new ArgumentOutOfRangeException(
                $"Search type {request.SearchType} must be SearchType.Coordinates for this request"
            );

        return await GetRequest<CensusGeocodingResponse>(
            BuildQueryParams(request, BuildReverseGeocodeQueryParams),
            $"{request.ReturnType.ToString().ToLowerInvariant()}/{request.SearchType.ToString().ToLowerInvariant()}"
        );
    }

    public void BuildGeocodeQueryParams(CensusGeocodingRequest request, StringBuilder builder)
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

            case SearchType.Coordinates:
            default:
                throw new ArgumentOutOfRangeException(
                    $"Search type {request.SearchType} is not valid for this request"
                );
        }
    }

    public void BuildReverseGeocodeQueryParams(
        CensusReverseGeocodingRequest request,
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

        if (builder[^1] == '&')
            builder.Remove(builder.Length - 1, 1);
    }
}