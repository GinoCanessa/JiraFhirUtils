namespace jira_fhir_cli.LlmProvider.Utils;

public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries, 
        int delaySeconds,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                // Calculate exponential backoff delay
                TimeSpan delay = TimeSpan.FromSeconds(delaySeconds * Math.Pow(2, attempt));
                
                Console.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}. Retrying in {delay.TotalSeconds} seconds...");
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        // This should not be reached due to the exception filtering above,
        // but we need to satisfy the compiler
        throw new InvalidOperationException("All retry attempts failed");
    }
}