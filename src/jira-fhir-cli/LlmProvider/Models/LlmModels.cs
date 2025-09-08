namespace jira_fhir_cli.LlmProvider.Models;

public record class LlmRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string Model { get; init; } = "";
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 1000;
    public Dictionary<string, object> AdditionalParameters { get; init; } = new();
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
    OpenAI,
    OpenAICompatible,
    Anthropic,
    Ollama,
    AzureOpenAI,
    GoogleAI
}