namespace jira_fhir_cli.LlmProvider.Utils;

public static class TokenCounter
{
    /// <summary>
    /// Estimates the number of tokens in a text string.
    /// This is a rough estimation based on the common rule of ~4 characters per token.
    /// For more accurate counting, you would need to use the specific tokenizer for the model.
    /// </summary>
    /// <param name="text">The text to estimate tokens for</param>
    /// <returns>Estimated number of tokens</returns>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
            
        // Rough estimation: ~4 characters per token
        // This varies by tokenizer but is a reasonable approximation
        return (int)Math.Ceiling(text.Length / 4.0);
    }
    
    /// <summary>
    /// Checks if the estimated token count exceeds the specified limit.
    /// </summary>
    /// <param name="text">The text to check</param>
    /// <param name="maxTokens">The maximum token limit</param>
    /// <returns>True if the text likely exceeds the token limit</returns>
    public static bool ExceedsTokenLimit(string text, int maxTokens)
    {
        return EstimateTokens(text) > maxTokens;
    }
    
    /// <summary>
    /// Truncates text to approximately fit within the token limit.
    /// This is a rough truncation and may not be exact.
    /// </summary>
    /// <param name="text">The text to truncate</param>
    /// <param name="maxTokens">The maximum token limit</param>
    /// <returns>Truncated text that should fit within the token limit</returns>
    public static string TruncateToTokenLimit(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        int estimatedTokens = EstimateTokens(text);
        if (estimatedTokens <= maxTokens)
            return text;
            
        // Calculate approximate character limit
        int targetCharLength = maxTokens * 4;
        
        if (text.Length <= targetCharLength)
            return text;
            
        // Truncate to target length and try to end at a word boundary
        string truncated = text.Substring(0, Math.Min(targetCharLength, text.Length));
        
        // Try to end at a word boundary
        int lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > targetCharLength * 0.8) // Don't truncate too much to find a space
        {
            truncated = truncated.Substring(0, lastSpace);
        }
        
        return truncated + "...";
    }
    
    /// <summary>
    /// Provides a more detailed token estimation breakdown for debugging purposes.
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <returns>A breakdown of the token estimation</returns>
    public static TokenEstimation GetDetailedEstimation(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TokenEstimation
            {
                Text = text ?? "",
                CharacterCount = 0,
                EstimatedTokens = 0,
                WordCount = 0
            };
        }
        
        int wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        return new TokenEstimation
        {
            Text = text,
            CharacterCount = text.Length,
            EstimatedTokens = EstimateTokens(text),
            WordCount = wordCount
        };
    }
}

public record class TokenEstimation
{
    public required string Text { get; init; }
    public required int CharacterCount { get; init; }
    public required int EstimatedTokens { get; init; }
    public required int WordCount { get; init; }
}