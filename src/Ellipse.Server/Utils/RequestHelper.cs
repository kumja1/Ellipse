
public static class RequestHelper
{

    public async static Task<TResult> RetryIfInvalid<TResult>(Func<TResult, bool> isValid, Func<int, Task<TResult>> func, TResult defaultValue = default, int maxRetries = 3, int delayMs = 100)
    {
        int retries = 0;
        TResult value = defaultValue;
        while (retries < maxRetries && !isValid(value))
        {
            try
            {
                value = await func(retries);
                if (!isValid(value))
                    await Task.Delay(delayMs * 2 ^ ++retries);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        Console.WriteLine($"Max retries reached. Returning default value.");
        return value;
    }
}