using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace Ellipse.Server.Policies;

public sealed class PostCachingPolicy : IOutputCachePolicy
{
    ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        var attemptOutputCaching = AttemptOutputCaching(context);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;

        // Vary by any query by default
        context.CacheVaryByRules.QueryKeys = "*";
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        HttpResponse response = context.HttpContext.Response;

        // Verify existence of cookie headers
        if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        // Check response code
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool AttemptOutputCaching(OutputCacheContext context)
    {
        // Check if the current request fulfills the requirements
        // to be cached
        HttpRequest request = context.HttpContext.Request;

        // Verify the method
        if (!HttpMethods.IsPost(request.Method))
            return false;

        if (
            !StringValues.IsNullOrEmpty(request.Headers.Authorization)
            || request.HttpContext.User?.Identity?.IsAuthenticated == true
        )
            return false;

        if (
            request.Query.TryGetValue("overwriteCache", out StringValues overwriteCacheValues)
            && overwriteCacheValues.Contains("true", StringComparer.OrdinalIgnoreCase)
        )
            return false;

        return true;
    }
}
