using System.Globalization;
using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models.Geocoding.PhotonGeocoder;

namespace Ellipse.Server.Utils.Clients.Mapping.Geocoding;

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

        return await GetRequest<PhotonGeocodingResponse>(
            BuildQueryParams(request, BuildGeocodeQueryParams),
            "api"
        );
    }

    public async Task<PhotonGeocodingResponse> ReverseGeocode(
        PhotonReverseGeocodingRequest request
    ) =>
        await GetRequest<PhotonGeocodingResponse>(
            BuildQueryParams(request, BuildReverseGeocodeQueryParams),
            "reverse"
        );

    public void BuildGeocodeQueryParams(PhotonGeocodingRequest request, StringBuilder builder)
    {
        AppendParam(builder, "q", request.Query);
        AppendParam(builder, "limit", request.Limit.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "lang", request.Lang);
        if (request is { Layers.Length: > 0 })
            AppendParam(builder, "osm_tag", string.Join(',', request.Layers));
    }

    public void BuildReverseGeocodeQueryParams(
        PhotonReverseGeocodingRequest request,
        StringBuilder builder
    )
    {
        AppendParam(builder, "lat", request.Latitude.ToString(CultureInfo.InvariantCulture));
        AppendParam(
            builder,
            "lon",
            request.Longitude.ToString(CultureInfo.InvariantCulture),
            false
        );
    }
}
