namespace Ellipse.Common.Utils;

public static class CallbackHelper
{
    public static async Task<TResult?> RetryIfInvalid<TResult>(
        Func<TResult?, bool>? isValid,
        Func<int, Task<TResult>> func,
        TResult? defaultValue = default,
        int maxRetries = 5,
        int delayMs = 100
    )
    {
        int retries = 0;
        TResult? value = defaultValue;

        isValid ??= v => v != null && !v.Equals(defaultValue);
        while (retries < maxRetries && !isValid(value))
        {
            try
            {
                value = await func(retries);
                if (!isValid(value))
                    Console.WriteLine($"Retrying... Attempt {retries + 1} of {maxRetries}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error: {ex.Message} - Retrying... Attempt {retries + 1} of {maxRetries}. StackTrace: {ex.StackTrace}"
                );
            }

            retries++;
            await Task.Delay((int)(delayMs * Math.Pow(2, retries)));
        }

        if (retries >= maxRetries)
            Console.WriteLine($"Max retries reached. Returning default value: {defaultValue}");

        return value;
    }
}
