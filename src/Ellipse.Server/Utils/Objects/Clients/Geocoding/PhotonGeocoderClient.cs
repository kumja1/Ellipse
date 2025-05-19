using System.Globalization;
using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models.Geocoding.PhotonGeocoder;

namespace Ellipse.Server.Utils.Objects.Clients.Geocoding;

public sealed class PhotonGeocoderClient(HttpClient client)
    : WebClient(client, "https://photon.komoot.io"),
        IGeocoderClient<
            PhotonGeocodingRequest,
            PhotonReverseGeocodingRequest,
            PhotonGeocodingResponse
        >
{
    public async Task<PhotonGeocodingResponse> Geocode(PhotonGeocodingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ArgumentException("Query must be provided.", nameof(request.Query));

        return await GetRequestAsync<PhotonGeocodingRequest, PhotonGeocodingResponse>(
            parameters: BuildQueryParams(request, BuildGeocodeQueryParams),
            additionalArgs: "api"
        );
    }

    public async Task<PhotonGeocodingResponse> ReverseGeocode(
        PhotonReverseGeocodingRequest request
    ) =>
        await GetRequestAsync<PhotonGeocodingRequest, PhotonGeocodingResponse>(
            parameters: BuildQueryParams(request, BuildReverseGeocodeQueryParams),
            additionalArgs: "reverse"
        );

    public string BuildGeocodeQueryParams(PhotonGeocodingRequest request, StringBuilder builder)
    {
        AppendParam(builder, "q", request.Query);
        AppendParam(builder, "limit", request.Limit.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "lang", request.Lang);
        if (request != null && request.Layers?.Length > 0)
            AppendParam(builder, "osm_tag", string.Join(',', request.Layers));

        return builder.ToString().TrimEnd('&');
    }

    public string BuildReverseGeocodeQueryParams(
        PhotonReverseGeocodingRequest request,
        StringBuilder builder
    )
    {
        AppendParam(builder, "lat", request.Latitude.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "lon", request.Longitude.ToString(CultureInfo.InvariantCulture));
        return builder.ToString().TrimEnd('&');
    }
}
