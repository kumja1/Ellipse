using System.Globalization;
using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Models.Snapping.OpenRoute;

namespace Ellipse.Server.Utils.Clients.Mapping;

public sealed class OpenRouteClient(HttpClient client, string apiKey)
    : WebClient(client, "https://api.openrouteservice.org", apiKey),
        IGeocoderClient<
            OpenRouteGeocodingRequest,
            OpenRouteReverseGeocodingRequest,
            OpenRouteGeocodingResponse
        >,
        ISnappingClient<OpenRouteSnappingRequest, OpenRouteSnappingResponse>,
        IMatrixClient<OpenRouteMatrixRequest, OpenRouteMatrixResponse>
{
    public async Task<OpenRouteGeocodingResponse> Geocode(OpenRouteGeocodingRequest request)
    {
        string queryParams = BuildQueryParams(request, BuildGeocodeQueryParams);
        return await GetRequest<OpenRouteGeocodingResponse>(queryParams, "geocode/search");
    }

    public async Task<OpenRouteGeocodingResponse> ReverseGeocode(
        OpenRouteReverseGeocodingRequest request
    )
    {
        string queryParams = BuildQueryParams(request, BuildReverseGeocodeQueryParams);
        return await GetRequest<OpenRouteGeocodingResponse>(queryParams, "geocode/reverse");
    }

    public string BuildGeocodeQueryParams(OpenRouteGeocodingRequest request, StringBuilder builder)
    {
        AppendParam(builder, "api_key", apiKey);
        AppendParam(builder, "text", request.Query);
        AppendParam(builder, "size", request.Size.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public string BuildReverseGeocodeQueryParams(
        OpenRouteReverseGeocodingRequest request,
        StringBuilder builder
    )
    {
        AppendParam(builder, "api_key", apiKey);
        AppendParam(builder, "point.lon", request.Longitude.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "point.lat", request.Latitude.ToString(CultureInfo.InvariantCulture));
        AppendParam(builder, "size", request.Size.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    public Task<OpenRouteSnappingResponse> SnapToRoads(
        OpenRouteSnappingRequest request,
        Profile profile
    ) =>
        PostRequestAsync<OpenRouteSnappingRequest, OpenRouteSnappingResponse>(
            request,
            "v2/snap",
            profile.AsString()
        );

    public async Task<OpenRouteMatrixResponse> GetMatrix(OpenRouteMatrixRequest request) =>
        await PostRequestAsync<OpenRouteMatrixRequest, OpenRouteMatrixResponse>(
            request,
            "v2/matrix",
            request.Profile.AsString()
        );
}
