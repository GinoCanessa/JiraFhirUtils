namespace jira_fhir_cli.LlmProvider;

public record class LlmRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string Model { get; init; } = "";
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 1000;
    public Dictionary<string, object> AdditionalParameters { get; init; } = [];
}

public record class LlmResponse
{
    public required string Content { get; init; }
    public string? Model { get; init; }
    public int? TokensUsed { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum LlmProviderType
{
    OpenAi,
    OpenAiCompatible,
    // Anthropic,
    Ollama,
    AzureOpenAi,
    GoogleAi,
}

public static class LlmModelExtensions
{
    public static LlmProviderType ToLlmProvider(this string provider) =>
        provider.ToLowerInvariant() switch
        {
            "openai" => LlmProviderType.OpenAi,
            "azure" => LlmProviderType.AzureOpenAi,
            "azureopenai" => LlmProviderType.AzureOpenAi,
            // "anthropic" => LlmProviderType.Anthropic,
            "ollama" => LlmProviderType.Ollama,
            "google" => LlmProviderType.GoogleAi,
            "googleai" => LlmProviderType.GoogleAi,
            _ => LlmProviderType.OpenAiCompatible
        };
}

public static class PromptTemplates
{
    public const string IssuePrompt = """
       Summarize this JIRA issue in 2-3 sentences, focusing on the core problem and key details:

       Title: {title}
       Description: {description}";
       """;
    public const string CommentPrompt = """
        Summarize the key points from these JIRA comments in 2-3 sentences:

        {comments}";
        """;

    public const string ResolutionPrompt = """
        Summarize the resolution of this JIRA issue in 1-2 sentences:

        Resolution: {resolution}
        Description: {resolutionDescription}";
        """;
}