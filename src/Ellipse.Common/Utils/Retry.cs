using System.Collections;
using Serilog;

namespace Ellipse.Common.Utils;

public static class Retry
{
    public static async Task Default<TResult>(Func<int, Task<TResult>> func, int maxRetries = 5,
        int delayMs = 100
    )
        => await RetryIfInvalid(null, func, default, maxRetries, delayMs);

    public static async Task<TItem[]> RetryIfCollectionEmpty<TItem>(Func<int, Task<TItem[]>> func,
        int maxRetries = 5,
        int delayMs = 100) =>
        await RetryIfInvalid(a => a.Length > 0, func, [], maxRetries, delayMs);

    public static async Task<List<TItem>> RetryIfCollectionEmpty<TItem>(Func<int, Task<List<TItem>>> func,
        int maxRetries = 5,
        int delayMs = 100) => await RetryIfInvalid(l => l.Count > 0, func, [], maxRetries, delayMs);

    public static async Task<HttpResponseMessage?> RetryIfResponseFailed(Func<int, Task<HttpResponseMessage>> func,
        int maxRetries = 5,
        int delayMs = 100) =>
        await RetryIfInvalid(message => message is { IsSuccessStatusCode: true }, func, null, maxRetries,
            delayMs);

    public static async Task<TResult?> RetryIfInvalid<TResult>(
        Func<TResult?, bool>? isValid,
        Func<int, Task<TResult>> func,
        TResult? defaultValue = default,
        int maxRetries = 5,
        int delayMs = 100,
        int maxDelayMs = 30000
    )
    {
        int retries = 0;
        TResult? value = defaultValue;

        isValid ??= v => v != null && !v.Equals(defaultValue);

        while (retries < maxRetries)
        {
            try
            {
                value = await func(retries);
                if (isValid(value))
                    return value;

                Log.Warning("Attempt {Attempt} of {MaxRetries} failed.", retries + 1, maxRetries);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error on attempt {Attempt} of {MaxRetries}: {Message}", retries + 1, maxRetries,
                    ex.Message);
            }

            retries++;
            if (retries < maxRetries)
            {
                int delay = (int)Math.Min(maxDelayMs, delayMs * Math.Pow(2, retries));
                Log.Information("Retrying in {Delay}ms...", delay);
                await Task.Delay(delay);
            }
        }

        Log.Warning("Max retries reached. Returning last value.");

        return value;
    }
}