using System.ClientModel;
using System.Text;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;
using jira_fhir_cli.LlmProvider.Utils;

namespace jira_fhir_cli.LlmProvider.Providers;

public class OpenAICompatibleProvider : ILlmProvider
{
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly LlmConfiguration _config;
    
    public OpenAICompatibleProvider(LlmConfiguration config)
    {
        _config = config;
        
        // Configure OpenAI client options
        OpenAIClientOptions options = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
        };
        
        // Set custom endpoint if not using OpenAI directly
        if (!string.IsNullOrEmpty(config.ApiEndpoint) && 
            config.ApiEndpoint != "https://api.openai.com/v1")
        {
            options.Endpoint = new Uri(config.ApiEndpoint.TrimEnd('/'));
        }
        
        // Initialize OpenAI client
        ApiKeyCredential credential = new ApiKeyCredential(config.ApiKey ?? string.Empty);
        _client = new OpenAIClient(credential, options);
        _chatClient = _client.GetChatClient(_config.Model);
    }
    
    public string ProviderName => "OpenAI Compatible";
    public bool SupportsStreaming => true;
    
    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            () => generateInternalAsync(request, cancellationToken),
            _config.MaxRetries,
            _config.RetryDelaySeconds,
            cancellationToken);
    }
    
    private async Task<LlmResponse> generateInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Build chat messages using SDK types
            List<ChatMessage> messages = [];
            
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));
            }
            
            messages.Add(ChatMessage.CreateUserMessage(request.Prompt));

            // Configure chat completion options
            ChatCompletionOptions options = new ChatCompletionOptions
            {
                Temperature = (float)request.Temperature
                // Note: MaxTokens configuration may need to be set differently in this SDK version
            };

            // Use appropriate chat client (either with specific model or the configured one)
            ChatClient chatClient = _chatClient;
            if (!string.IsNullOrEmpty(request.Model) && request.Model != _config.Model)
            {
                chatClient = _client.GetChatClient(request.Model);
            }

            // Make the request using the SDK
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

            // Extract response data
            string responseContent = completion.Content[0].Text;
            string? model = completion.Model;
            int? tokensUsed = completion.Usage?.TotalTokenCount;

            return new LlmResponse
            {
                Content = responseContent,
                Model = model,
                TokensUsed = tokensUsed,
                Success = true
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = "Request was cancelled"
            };
        }
        catch (TaskCanceledException)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Request timed out after {_config.RequestTimeoutSeconds} seconds"
            };
        }
        catch (Exception ex)
        {
            // Handle all other exceptions (including SDK exceptions)
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Request failed: {ex.Message}"
            };
        }
    }
    
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LlmRequest testRequest = new LlmRequest
            {
                Prompt = "Test",
                MaxTokens = 10,
                Temperature = 0.1
            };
            
            LlmResponse response = await GenerateAsync(testRequest, cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LLM connection validation failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        // OpenAI client doesn't implement IDisposable in this version
        // No cleanup needed
    }
}