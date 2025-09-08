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
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 2;
    
    // Azure OpenAI specific properties
    public string? DeploymentName { get; init; } = null;
    public string? ResourceName { get; init; } = null;
    public bool UseDefaultAzureCredential { get; init; } = false;
    
    public Dictionary<string, object> ProviderSpecificSettings { get; init; } = new();
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