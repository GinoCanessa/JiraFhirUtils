using jira_fhir_cli.LlmProvider.Models;

namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// Service interface for Microsoft Semantic Kernel integration that maintains compatibility 
/// with the existing LlmRequest/LlmResponse patterns while providing SK-based implementations.
/// </summary>
public interface ISemanticKernelService
{
    /// <summary>
    /// Generate a response using the configured Semantic Kernel chat completion service
    /// </summary>
    /// <param name="request">LLM request containing prompt, system message, and parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LLM response with generated content</returns>
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate that the SK service is properly configured and can connect to the provider
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is successful</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the name of the provider (e.g., "OpenAI", "Azure OpenAI", "Anthropic")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Indicates whether the provider supports streaming responses
    /// </summary>
    bool SupportsStreaming { get; }
    
    /// <summary>
    /// Gets the underlying kernel instance for advanced scenarios
    /// </summary>
    Microsoft.SemanticKernel.Kernel Kernel { get; }
}