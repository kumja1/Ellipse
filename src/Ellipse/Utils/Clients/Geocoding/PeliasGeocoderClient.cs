using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Geocoding.Pelias;

namespace Ellipse.Utils.Clients.Geocoding;

public class PeliasGeocoderClient(HttpClient client, string apiKey) : WebClient(client, "https://api.heigit.org/pelias", apiKey), IGeocoderClient<PeliasGeocodingRequest, PeliasReverseGeocodingRequest, PeliasGeocodingResponse>
{
  
    public async Task<PeliasGeocodingResponse> Geocode(PeliasGeocodingRequest request)
    {
        string queryParams = BuildQueryParams(request, BuildGeocodeQueryParams);
        return await GetRequest<PeliasGeocodingResponse>(queryParams, "v1/search");
    }

    public async Task<PeliasGeocodingResponse> ReverseGeocode(
        PeliasReverseGeocodingRequest request
    )
    {
        string queryParams = BuildQueryParams(request, BuildReverseGeocodeQueryParams);
        return await GetRequest<PeliasGeocodingResponse>(queryParams, "v1/reverse");
    }

    public void BuildGeocodeQueryParams(PeliasGeocodingRequest request, StringBuilder builder)
    {
        AppendParam(builder, "api_key", apiKey);
        AppendParam(builder, "text", request.Query);
        AppendParam(builder, "size", request.Size);
    }

    public void BuildReverseGeocodeQueryParams(
        PeliasReverseGeocodingRequest request,
        StringBuilder builder
    )
    {
        AppendParam(builder, "api_key", apiKey);
        AppendParam(builder, "point.lon", request.Longitude);
        AppendParam(builder, "point.lat", request.Latitude);
        AppendParam(builder, "size", request.Size);
        AppendParam(builder, "layers", request.Layers);
        AppendParam(builder, "boundary.country", request.BoundaryCountry);
        AppendParam(builder, "boundary.circle.radius", request.BoundaryCircleRadius);
    }
}