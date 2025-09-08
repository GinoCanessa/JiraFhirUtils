using jira_fhir_cli.LlmProvider.Models;

namespace jira_fhir_cli.LlmProvider;

public interface ILlmProvider
{
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    string ProviderName { get; }
    bool SupportsStreaming { get; }
}