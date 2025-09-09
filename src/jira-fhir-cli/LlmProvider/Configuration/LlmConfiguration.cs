using jira_fhir_cli.LlmProvider.Models;

namespace jira_fhir_cli.LlmProvider.Configuration;

public record class LlmConfiguration
{
    public required LlmProviderType ProviderType { get; init; }
    public required string ApiEndpoint { get; init; }
    public string? ApiKey { get; init; }
    public required string Model { get; init; }
    public string? ApiVersion { get; init; } = null;
    public double Temperature { get; init; } = 0.3; // Lower for consistent summaries
    public int MaxTokens { get; init; } = 500;
    
    // Azure OpenAI specific properties
    public string? DeploymentName { get; init; } = null;
    public string? ResourceName { get; init; } = null;
    public bool UseDefaultAzureCredential { get; init; } = false;
    
    // Semantic Kernel specific settings (replaces custom retry/timeout settings)
    public SemanticKernelSettings? SemanticKernelSettings { get; init; } = null;
    
    public Dictionary<string, object> ProviderSpecificSettings { get; init; } = new();
}

/// <summary>
/// Configuration settings specific to Microsoft Semantic Kernel integration
/// </summary>
public record class SemanticKernelSettings
{
    /// <summary>
    /// Enable detailed logging for SK operations
    /// </summary>
    public bool EnableDetailedLogging { get; init; } = false;
    
    /// <summary>
    /// Custom service ID for the chat completion service (optional)
    /// </summary>
    public string? ServiceId { get; init; } = null;
    
    /// <summary>
    /// Retry policy configuration for SK HTTP requests
    /// </summary>
    public SemanticKernelRetryPolicy? RetryPolicy { get; init; } = null;
    
    /// <summary>
    /// Custom HTTP client settings
    /// </summary>
    public SemanticKernelHttpSettings? HttpSettings { get; init; } = null;
    
    /// <summary>
    /// Advanced kernel configuration options
    /// </summary>
    public Dictionary<string, object> KernelSettings { get; init; } = new();
}

/// <summary>
/// Retry policy settings for Semantic Kernel
/// </summary>
public record class SemanticKernelRetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts (defaults to 3 if not specified)
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;
    
    /// <summary>
    /// Initial delay between retries in seconds
    /// </summary>
    public double InitialDelaySeconds { get; init; } = 2.0;
    
    /// <summary>
    /// Maximum delay between retries in seconds
    /// </summary>
    public double MaxDelaySeconds { get; init; } = 60.0;
    
    /// <summary>
    /// Backoff multiplier for exponential backoff
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;
    
    /// <summary>
    /// Enable jitter to prevent thundering herd
    /// </summary>
    public bool UseJitter { get; init; } = true;
}

/// <summary>
/// HTTP client settings for Semantic Kernel
/// </summary>
public record class SemanticKernelHttpSettings
{
    /// <summary>
    /// HTTP request timeout in seconds (defaults to 30 seconds if not specified)
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;
    
    /// <summary>
    /// Custom user agent for HTTP requests
    /// </summary>
    public string? UserAgent { get; init; } = null;
    
    /// <summary>
    /// Additional HTTP headers to include with requests
    /// </summary>
    public Dictionary<string, string> AdditionalHeaders { get; init; } = new();
}

public record class SummaryConfiguration
{
    public required LlmConfiguration LlmConfig { get; init; }
    public int BatchSize { get; init; } = 10;
    public bool OverwriteExistingSummaries { get; init; } = false;
    public SummaryTypes SummaryTypesToGenerate { get; init; } = SummaryTypes.All;
    public PromptTemplates Prompts { get; init; } = new();
}

[Flags]
public enum SummaryTypes
{
    Issue = 1,
    Comments = 2, 
    Resolution = 4,
    All = Issue | Comments | Resolution
}

public record class PromptTemplates
{
    public string IssuePrompt { get; init; } = "Summarize this JIRA issue in 2-3 sentences, focusing on the core problem and key details:\n\nTitle: {title}\nDescription: {description}";
    public string CommentPrompt { get; init; } = "Summarize the key points from these JIRA comments in 2-3 sentences:\n\n{comments}";
    public string ResolutionPrompt { get; init; } = "Summarize the resolution of this JIRA issue in 1-2 sentences:\n\nResolution: {resolution}\nDescription: {resolutionDescription}";
}