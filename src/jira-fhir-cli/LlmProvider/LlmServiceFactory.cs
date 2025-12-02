using OpenAI;
using OpenAI.Chat;
using Azure.Identity;
using System.ClientModel;

namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// Factory for creating OpenAI SDK-based LLM services that replace the legacy SemanticKernel-based factory.
/// Supports OpenAI, Azure OpenAI, and OpenAI-compatible providers through the OpenAI SDK.
/// </summary>
public static class LlmServiceFactory
{
    /// <summary>
    /// Creates a new ILlmService instance based on the provided configuration
    /// </summary>
    /// <param name="config">LLM configuration specifying provider type and settings</param>
    /// <returns>Configured LLM service</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider type is not supported</exception>
    public static ILlmService CreateService(CliConfig config)
    {
        LlmProviderType pt = string.IsNullOrEmpty(config.LlmProvider)
            ? LlmProviderType.OpenAiCompatible
            : config.LlmProvider.ToLlmProvider();
        
        return pt switch
        {
            LlmProviderType.OpenAi => 
                createOpenAiService(config),
            LlmProviderType.OpenAiCompatible => 
                createOpenAiCompatibleService(config),
            _ => throw new NotSupportedException($"Provider {config.LlmProvider} not supported. Supported providers: openai, azureopenai, openai-compatible")
        };
    }
    
    /// <summary>
    /// Creates an OpenAI service using the official OpenAI API
    /// </summary>
    private static ILlmService createOpenAiService(CliConfig config)
    {
        validateCommonConfig(config);
        
        if (string.IsNullOrEmpty(config.LlmApiKey))
        {
            throw new ArgumentException("LLM API Key is required for OpenAI");
        }
        
        OpenAIClientOptions options = new();
        
        // Configure logging if in debug mode
        if (config.DebugMode)
        {
            // OpenAI SDK logging configuration would go here if needed
        }
        
        OpenAIClient client = new(new ApiKeyCredential(config.LlmApiKey), options);
        ChatClient chatClient = client.GetChatClient(config.LlmModel!);
        
        return new OpenAiLlmService(chatClient, config);
    }
    
    /// <summary>
    /// Creates an OpenAI-compatible service for custom endpoints (OpenRouter, LMStudio, etc.)
    /// </summary>
    private static ILlmService createOpenAiCompatibleService(CliConfig config)
    {
        validateCommonConfig(config);
        
        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri(config.LlmApiEndpoint!),
        };
        
        // Configure logging if in debug mode
        if (config.DebugMode)
        {
            // OpenAI SDK logging configuration would go here if needed
        }
        
        // Some OpenAI-compatible services might not require a real API key
        string apiKey = config.LlmApiKey ?? "not-a-key";
        
        OpenAIClient client = new(new ApiKeyCredential(apiKey), options);
        ChatClient chatClient = client.GetChatClient(config.LlmModel!);
        
        return new OpenAiLlmService(chatClient, config);
    }
    
    
    /// <summary>
    /// Validates common configuration requirements
    /// </summary>
    private static void validateCommonConfig(CliConfig config)
    {
        if (string.IsNullOrEmpty(config.LlmProvider))
        {
            throw new ArgumentException("LLM Provider not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            throw new ArgumentException("LLM Endpoint not specified");
        }

        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("LLM Model not specified");
        }
    }
}