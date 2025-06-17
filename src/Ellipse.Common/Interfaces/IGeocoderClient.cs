using System.Text;

namespace Ellipse.Common.Interfaces;

public interface IGeocoderClient<TRequest, TReverseRequest, TResponse>
{
    protected string BuildGeocodeQueryParams(TRequest request, StringBuilder builder);
    protected string BuildReverseGeocodeQueryParams(TReverseRequest request, StringBuilder builder);

    public Task<TResponse> Geocode(TRequest request);
    public Task<TResponse> ReverseGeocode(TReverseRequest request);
}
