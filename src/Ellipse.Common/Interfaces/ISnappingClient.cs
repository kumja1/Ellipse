using Ellipse.Common.Models;

namespace Ellipse.Common.Interfaces;

public interface ISnappingClient<TRequest, TResponse>
{
    public Task<TResponse> SnapToRoads(TRequest request, Profile profile);
}
