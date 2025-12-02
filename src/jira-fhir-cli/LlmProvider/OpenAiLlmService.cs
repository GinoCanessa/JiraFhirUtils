using OpenAI;
using OpenAI.Chat;

namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// OpenAI SDK-based LLM service implementation that provides direct integration 
/// with OpenAI and OpenAI-compatible APIs.
/// </summary>
public class OpenAiLlmService : ILlmService
{
    private readonly ChatClient _chatClient;
    private readonly CliConfig _config;

    public OpenAiLlmService(ChatClient chatClient, CliConfig config)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(_config.LlmProvider))
        {
            throw new ArgumentException($"{nameof(_config.LlmProvider)} cannot be null or empty");
        }

        if (string.IsNullOrEmpty(_config.LlmApiEndpoint))
        {
            throw new ArgumentException("LlmApiEndpoint must be specified in the configuration.", nameof(config));
        }
        
        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("LlmModel must be specified in the configuration.", nameof(config));
        }
    }

    public string ProviderName => _config.LlmProvider!;
    
    /// <summary>
    /// OpenAI supports streaming, though we're not currently using it
    /// </summary>
    public bool SupportsStreaming => true;

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create chat messages from the request
            List<ChatMessage> messages = createChatMessages(request);
            
            // Create chat completion options
            ChatCompletionOptions options = createChatCompletionOptions(request);
            
            // Get chat completion from OpenAI
            ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            
            // Convert result back to LlmResponse
            return createLlmResponse(completion, request);
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
                Model = _config.LlmModel!,
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
    /// Converts LlmRequest to OpenAI ChatMessage list
    /// </summary>
    private static List<ChatMessage> createChatMessages(LlmRequest request)
    {
        List<ChatMessage> messages = [];
        
        // Add system message if provided
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));
        }
        
        // Add user message
        messages.Add(ChatMessage.CreateUserMessage(request.Prompt));
        
        return messages;
    }

    /// <summary>
    /// Creates OpenAI ChatCompletionOptions from LlmRequest and configuration
    /// </summary>
    private ChatCompletionOptions createChatCompletionOptions(LlmRequest request)
    {
        ChatCompletionOptions options = new()
        {
            Temperature = request.Temperature > 0 ? (float)request.Temperature : (float)_config.LlmTemperature,
            MaxOutputTokenCount = request.MaxTokens > 0 ? request.MaxTokens : _config.LlmMaxTokens
        };
        
        // Add any additional parameters from the request
        // Note: The OpenAI SDK has specific properties, so we'll map common ones
        foreach (var kvp in request.AdditionalParameters)
        {
            switch (kvp.Key.ToLowerInvariant())
            {
                case "top_p":
                    if (kvp.Value is double topP)
                        options.TopP = (float)topP;
                    break;
                case "frequency_penalty":
                    if (kvp.Value is double freqPenalty)
                        options.FrequencyPenalty = (float)freqPenalty;
                    break;
                case "presence_penalty":
                    if (kvp.Value is double presPenalty)
                        options.PresencePenalty = (float)presPenalty;
                    break;
                // Add more parameter mappings as needed
            }
        }
        
        return options;
    }

    /// <summary>
    /// Converts OpenAI ChatCompletion to LlmResponse
    /// </summary>
    private static LlmResponse createLlmResponse(ChatCompletion completion, LlmRequest originalRequest)
    {
        Dictionary<string, object> metadata = new()
        {
            ["CompletionId"] = completion.Id ?? "Unknown",
            ["Model"] = completion.Model ?? "Unknown",
            ["FinishReason"] = completion.FinishReason.ToString(),
            ["CreatedAt"] = completion.CreatedAt
        };
        
        // Extract token usage if available
        int? tokensUsed = null;
        if (completion.Usage != null)
        {
            tokensUsed = completion.Usage.InputTokenCount + completion.Usage.OutputTokenCount;
            metadata["Usage"] = new Dictionary<string, object>
            {
                ["InputTokens"] = completion.Usage.InputTokenCount,
                ["OutputTokens"] = completion.Usage.OutputTokenCount,
                ["TotalTokens"] = tokensUsed ?? 0
            };
        }
        
        // Get the content from the response
        string content = completion.Content?[0]?.Text ?? string.Empty;
        
        return new LlmResponse
        {
            Content = content,
            Success = true,
            Model = completion.Model ?? originalRequest.Model,
            TokensUsed = tokensUsed,
            Metadata = metadata
        };
    }
}