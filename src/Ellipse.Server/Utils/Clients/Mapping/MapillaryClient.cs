using System.Globalization;
using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models.Mapillary;

namespace Ellipse.Server.Utils.Clients.Mapping;

public sealed class MapillaryClient(HttpClient client, string apiKey)
    : WebClient(client, "https://graph.mapillary.com", apiKey), IMapillaryClient
{
    public async Task<MapillaryResponse<MapillaryImage>> SearchImages(MapillarySearchRequest request)
    {
        string queryParams = BuildQueryParams(request, BuildSearchQueryParams);
        return await GetRequest<MapillaryResponse<MapillaryImage>>(queryParams, "images");
    }

    private string BuildSearchQueryParams(MapillarySearchRequest request, StringBuilder builder)
    {
        if (request.MinLon.HasValue && request is { MinLat: not null, MaxLon: not null, MaxLat: not null })
            AppendParam(builder, "bbox", string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
                request.MinLon, request.MinLat, request.MaxLon, request.MaxLat));
        
        if (request.Limit.HasValue)
            AppendParam(builder, "limit", request.Limit);

        if (!string.IsNullOrEmpty(request.Fields))
            AppendParam(builder, "fields", request.Fields);


        return builder.ToString();
    }
}