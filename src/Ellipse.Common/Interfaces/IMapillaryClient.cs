using Ellipse.Common.Models.Mapillary;

namespace Ellipse.Common.Interfaces;

public interface IMapillaryClient
{
    Task<MapillaryResponse<MapillaryImage>> SearchImages(MapillarySearchRequest request);
}
