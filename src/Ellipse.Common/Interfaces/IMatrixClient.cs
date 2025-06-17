namespace Ellipse.Common.Interfaces;

public interface IMatrixClient<TRequest, TResponse>
{
    public Task<TResponse> GetMatrix(TRequest request);
}
