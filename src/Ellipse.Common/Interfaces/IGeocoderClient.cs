using System.Text;

namespace Ellipse.Common.Interfaces;

public interface IGeocoderClient<in TRequest, TReverseRequest, TResponse>
{
    string BuildGeocodeQueryParams(TRequest request, StringBuilder builder);
    string BuildReverseGeocodeQueryParams(TReverseRequest request, StringBuilder builder);

    public Task<TResponse> Geocode(TRequest request);
    public Task<TResponse> ReverseGeocode(TReverseRequest request);
}