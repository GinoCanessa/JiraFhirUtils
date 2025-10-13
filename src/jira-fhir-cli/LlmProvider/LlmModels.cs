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
}

public static class LlmModelExtensions
{
    public static LlmProviderType ToLlmProvider(this string provider) =>
        provider.ToLowerInvariant() switch
        {
            "openai" => LlmProviderType.OpenAi,
            _ => LlmProviderType.OpenAiCompatible
        };
}

public static class PromptTemplates
{
    public const string IssuePrompt = """
          Following is information entered by a user requesting a change to the FHIR specification.
          Summarize with a short and concise single paragraph of 2-3 sentences for review by the responsible Work Group.
          Knowledge of the FHIR specification can be assumed.
          <title>
          {title}
          </title>
          <description>
          {description}
          </description>
          """;
    public const string IssuePromptV0 = """
       Summarize this JIRA issue in 2-3 sentences, focusing on the core problem and key details:

       Title: {title}
       Description: {description}";
       """;

    public const string CommentPrompt = """
        Following are comments entered by users regarding a ticket requesting a change to the FHIR specification.
        Summarize with a short and concise single paragraph of 2-3 sentences for review by the responsible Work Group.
        Knowledge of the FHIR specification can be assumed.
        <comments>
        {comments}
        </comments>
        """;

    public const string CommentPromptV0 = """
        Summarize the key points from these JIRA comments in 2-3 sentences:

        {comments}
        """;

    public const string ResolutionPrompt = """
        Following is the resolution for a ticket requesting a change to the FHIR specification.
        Summarize with a short and concise single paragraph of 2-3 sentences for review by the responsible Work Group.
        Knowledge of the FHIR specification can be assumed.
        <resolution>
        {resolution}
        </resolution>
        <resolutionDescription>
        {resolutionDescription}
        </resolutionDescription>
        """;

    public const string ResolutionPromptV0 = """
        Summarize the resolution of this JIRA issue in 1-2 sentences:

        Resolution: {resolution}
        Description: {resolutionDescription}";
        """;
}