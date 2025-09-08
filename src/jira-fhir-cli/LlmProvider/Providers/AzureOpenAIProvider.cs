using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;
using jira_fhir_cli.LlmProvider.Utils;

namespace jira_fhir_cli.LlmProvider.Providers;

public class AzureOpenAIProvider : ILlmProvider
{
    private readonly AzureOpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly LlmConfiguration _config;
    private readonly string _deploymentName;
    
    public AzureOpenAIProvider(LlmConfiguration config)
    {
        _config = config;
        
        // Get Azure-specific settings from ProviderSpecificSettings
        _deploymentName = GetRequiredSetting("DeploymentName");
        string? resourceName = GetOptionalSetting("ResourceName");
        
        // Build Azure endpoint URL
        Uri endpoint;
        if (!string.IsNullOrEmpty(config.ApiEndpoint))
        {
            endpoint = new Uri(config.ApiEndpoint.TrimEnd('/'));
        }
        else if (!string.IsNullOrEmpty(resourceName))
        {
            endpoint = new Uri($"https://{resourceName}.openai.azure.com/");
        }
        else
        {
            throw new ArgumentException("Either ApiEndpoint or ResourceName must be provided for Azure OpenAI");
        }
        
        // Configure Azure OpenAI client options
        AzureOpenAIClientOptions options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
        {
            NetworkTimeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds),
        };
        
        // Initialize Azure OpenAI client with API key authentication
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for Azure OpenAI authentication");
        }
        AzureKeyCredential credential = new AzureKeyCredential(config.ApiKey);

        _client = new AzureOpenAIClient(endpoint, credential, options);
        
        _chatClient = _client.GetChatClient(_deploymentName);
    }
    
    public string ProviderName => "Azure OpenAI";
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
            };

            // Use appropriate chat client (either with specific deployment or the configured one)
            ChatClient chatClient = _chatClient;
            if (!string.IsNullOrEmpty(request.Model) && request.Model != _deploymentName)
            {
                // For Azure OpenAI, the "model" in the request should be treated as a deployment name
                chatClient = _client.GetChatClient(request.Model);
            }

            // Make the request using the Azure OpenAI SDK
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
        catch (RequestFailedException ex)
        {
            // Azure-specific exception handling
            string errorMessage = $"Azure OpenAI request failed: {ex.Message}";
            if (ex.Status == 429)
            {
                errorMessage = "Rate limit exceeded. Please try again later.";
            }
            else if (ex.Status == 401)
            {
                errorMessage = "Authentication failed. Please check your API key or credential configuration.";
            }
            else if (ex.Status == 400 && ex.Message.Contains("content_filter"))
            {
                errorMessage = "Content was filtered by Azure OpenAI safety systems.";
            }
            
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            // Handle all other exceptions
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
            Console.WriteLine($"Azure OpenAI connection validation failed: {ex.Message}");
            return false;
        }
    }
    
    private string GetRequiredSetting(string key)
    {
        if (_config.ProviderSpecificSettings.TryGetValue(key, out object? value) && 
            value?.ToString() is string stringValue && 
            !string.IsNullOrEmpty(stringValue))
        {
            return stringValue;
        }
        
        throw new ArgumentException($"Required Azure OpenAI setting '{key}' is missing or empty");
    }
    
    private string? GetOptionalSetting(string key)
    {
        if (_config.ProviderSpecificSettings.TryGetValue(key, out object? value))
        {
            return value?.ToString();
        }
        return null;
    }
}