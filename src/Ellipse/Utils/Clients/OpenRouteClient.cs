using System.Text;
using Ellipse.Common.Interfaces;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Models.Snapping.OpenRoute;

namespace Ellipse.Utils.Clients;

public sealed class OpenRouteClient(HttpClient client, string apiKey)
    : WebClient(client, "https://api.heigit.org/openrouteservice", apiKey),
        ISnappingClient<OpenRouteSnappingRequest, OpenRouteSnappingResponse>,
        IMatrixClient<OpenRouteMatrixRequest, OpenRouteMatrixResponse>
{
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
