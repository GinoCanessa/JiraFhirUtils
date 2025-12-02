namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// Service interface for LLM integration using OpenAI SDK that maintains compatibility 
/// with the existing LlmRequest/LlmResponse patterns.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generate a response using the configured LLM chat completion service
    /// </summary>
    /// <param name="request">LLM request containing prompt, system message, and parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LLM response with generated content</returns>
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate that the LLM service is properly configured and can connect to the provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is successful</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the name of the provider (e.g., "OpenAI", "Azure OpenAI", "OpenAI-Compatible")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Indicates whether the provider supports streaming responses
    /// </summary>
    bool SupportsStreaming { get; }
}