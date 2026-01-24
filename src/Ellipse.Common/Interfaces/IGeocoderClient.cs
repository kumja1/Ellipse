using System.Text;

namespace Ellipse.Common.Interfaces;

public interface IGeocoderClient<in TRequest, TReverseRequest, TResponse>
{
    void BuildGeocodeQueryParams(TRequest request, StringBuilder builder);
    void BuildReverseGeocodeQueryParams(TReverseRequest request, StringBuilder builder);

    public Task<TResponse> Geocode(TRequest request);
    public Task<TResponse> ReverseGeocode(TReverseRequest request);
}