using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// Adapter that provides a bridge between the existing custom LLM abstractions 
/// and Microsoft Semantic Kernel's ChatCompletion services.
/// </summary>
public class SemanticKernelServiceAdapter : ISemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly CliConfig _config;

    public SemanticKernelServiceAdapter(
        Kernel kernel, 
        IChatCompletionService chatCompletionService, 
        CliConfig config)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletionService = chatCompletionService ?? throw new ArgumentNullException(nameof(chatCompletionService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrEmpty(config.LlmProvider))
        {
            throw new ArgumentNullException(nameof(config.LlmProvider), "LlmProvider must be specified in the configuration.");
        }
    }

    public Kernel Kernel => _kernel;
    public string ProviderName => _config.LlmProvider!;
    
    /// <summary>
    /// For now, assume streaming is supported. Individual providers can override this.
    /// </summary>
    public bool SupportsStreaming => true;

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert LlmRequest to ChatHistory
            ChatHistory chatHistory = CreateChatHistory(request);
            
            // Create prompt execution settings from request and config
            PromptExecutionSettings settings = createExecutionSettings(request);
            
            // Get chat completion
            ChatMessageContent result = await _chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                settings, 
                kernel: _kernel,
                cancellationToken: cancellationToken);
            
            // Convert result back to LlmResponse
            return createLlmResponse(result, request);
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Content = string.Empty,
                Success = false,
                ErrorMessage = ex.Message,
                Model = request.Model,
                Metadata = new Dictionary<string, object>
                {
                    ["Exception"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? string.Empty
                }
            };
        }
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a simple test request
            LlmRequest testRequest = new LlmRequest
            {
                Prompt = "Hello",
                Model = _config.LlmModel ?? throw new ArgumentNullException(nameof(_config.LlmModel)),
                Temperature = 0.1,
                MaxTokens = 10
            };
            
            LlmResponse response = await GenerateAsync(testRequest, cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Converts LlmRequest to SK ChatHistory
    /// </summary>
    private static ChatHistory CreateChatHistory(LlmRequest request)
    {
        ChatHistory chatHistory = new ChatHistory();
        
        // Add system message if provided
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            chatHistory.AddSystemMessage(request.SystemPrompt);
        }
        
        // Add user message
        chatHistory.AddUserMessage(request.Prompt);
        
        return chatHistory;
    }

    /// <summary>
    /// Creates SK PromptExecutionSettings from LlmRequest and LlmConfiguration
    /// </summary>
    private PromptExecutionSettings createExecutionSettings(LlmRequest request)
    {
        // Use the model from request if specified, otherwise fall back to config
        string modelId = !string.IsNullOrEmpty(request.Model) ? request.Model : _config.LlmModel ?? throw new ArgumentNullException(nameof(_config.LlmModel));
        
        // Create base settings
        PromptExecutionSettings settings = new PromptExecutionSettings
        {
            ModelId = modelId,
            ExtensionData = new Dictionary<string, object>()
        };
        
        // Add temperature if specified in request, otherwise use config
        double temperature = request.Temperature > 0 ? request.Temperature : _config.LlmTemperature;
        settings.ExtensionData["temperature"] = temperature;
        
        // Add max tokens if specified in request, otherwise use config  
        int maxTokens = request.MaxTokens > 0 ? request.MaxTokens : _config.LlmMaxTokens;
        settings.ExtensionData["max_tokens"] = maxTokens;
        
        // Add any additional parameters from the request
        foreach (var kvp in request.AdditionalParameters)
        {
            settings.ExtensionData[kvp.Key] = kvp.Value;
        }
        
        return settings;
    }

    /// <summary>
    /// Converts SK ChatMessageContent to LlmResponse
    /// </summary>
    private static LlmResponse createLlmResponse(ChatMessageContent result, LlmRequest originalRequest)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            ["MessageId"] = result.InnerContent?.GetType().Name ?? "Unknown",
            ["Role"] = result.Role.ToString()
        };
        
        // Extract token usage if available
        int? tokensUsed = null;
        if (result.Metadata?.TryGetValue("Usage", out object? usage) == true)
        {
            // Different providers may structure usage differently
            // This is a general approach that may need provider-specific handling
            metadata["Usage"] = usage ?? "Unknown";
            
            // Try to extract total tokens if it's in a common format
            if (usage is Dictionary<string, object> usageDict)
            {
                if (usageDict.TryGetValue("total_tokens", out object? totalTokens) && 
                    totalTokens is int total)
                {
                    tokensUsed = total;
                }
                else if (usageDict.TryGetValue("TotalTokens", out object? totalTokensAlt) && 
                         totalTokensAlt is int totalAlt)
                {
                    tokensUsed = totalAlt;
                }
            }
        }
        
        // Add any other metadata from the result
        if (result.Metadata != null)
        {
            foreach (var kvp in result.Metadata)
            {
                metadata[$"SK_{kvp.Key}"] = kvp.Value ?? "Unknown";
            }
        }
        
        return new LlmResponse
        {
            Content = result.Content ?? string.Empty,
            Success = true,
            Model = originalRequest.Model,
            TokensUsed = tokensUsed,
            Metadata = metadata
        };
    }
}