using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;

namespace jira_fhir_cli.LlmProvider;

/// <summary>
/// Factory for creating Semantic Kernel-based LLM services that replace the legacy LlmProviderFactory.
/// Supports all major LLM providers through Microsoft Semantic Kernel connectors.
/// </summary>
public static class SemanticKernelServiceFactory
{
    /// <summary>
    /// Creates a new ISemanticKernelService instance based on the provided configuration
    /// </summary>
    /// <param name="config">LLM configuration specifying provider type and settings</param>
    /// <returns>Configured semantic kernel service</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider type is not supported</exception>
    public static ISemanticKernelService CreateService(CliConfig config)
    {
        LlmProviderType pt = string.IsNullOrEmpty(config.LlmProvider)
            ? LlmProviderType.OpenAiCompatible
            : config.LlmProvider.ToLlmProvider();
        
        return pt switch
        {
            LlmProviderType.OpenAi or LlmProviderType.OpenAiCompatible => 
                createOpenAiService(config),
            LlmProviderType.AzureOpenAi => 
                createAzureOpenAiService(config),
            LlmProviderType.Ollama => 
                createOllamaService(config),
            LlmProviderType.GoogleAi => 
                createGoogleAiService(config),
            _ => throw new NotSupportedException($"Provider {config.LlmProvider} not supported")
        };
    }
    
    private static ISemanticKernelService createOpenAiService(CliConfig config)
    {
        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("Llm Model not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            throw new ArgumentException("Llm API Endpoint not specified");
        }
        
        IKernelBuilder builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: config.LlmModel,
            apiKey: config.LlmApiKey ?? string.Empty, // Some local services don't require real API keys
            endpoint: new Uri(config.LlmApiEndpoint),
            serviceId: config.SemanticKernelServiceId);

        // Configure logging if enabled
        configureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config);
    }
    
    private static ISemanticKernelService createAzureOpenAiService(CliConfig config)
    {
        string deploymentName = config.LlmDeploymentName 
            ?? config.LlmModel 
            ?? throw new ArgumentException("Llm Model or Deployment Name must be specified for Azure OpenAI");
            
        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("Llm Model not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            throw new ArgumentException("Llm API Endpoint not specified");
        }
        
        IKernelBuilder builder = Kernel.CreateBuilder();

        if (!string.IsNullOrEmpty(config.LlmApiKey))
        {
            // Use API key authentication
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: config.LlmApiEndpoint,
                apiKey: config.LlmApiKey,
                serviceId: config.SemanticKernelServiceId);
        }
        else
        {
            // Use Azure Default Credential (Managed Identity, etc.)
            TokenCredential credential = new DefaultAzureCredential();
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: config.LlmApiEndpoint,
                credentials: credential,
                serviceId: config.SemanticKernelServiceId);
        }
        
        configureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config);
    }

    
    private static ISemanticKernelService createOllamaService(CliConfig config)
    {
        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("Llm Model not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            throw new ArgumentException("Llm API Endpoint not specified");
        }
        
        IKernelBuilder builder = Kernel.CreateBuilder();
        
        // Add Ollama chat completion service
        Uri endpoint = new Uri(config.LlmApiEndpoint.TrimEnd('/'));
        
        builder.AddOllamaChatCompletion(
            modelId: config.LlmModel,
            endpoint: endpoint,
            serviceId: config.SemanticKernelServiceId);
        
        configureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config);
    }
    
    private static ISemanticKernelService createGoogleAiService(CliConfig config)
    {
        if (string.IsNullOrEmpty(config.LlmModel))
        {
            throw new ArgumentException("Llm Model not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            throw new ArgumentException("Llm API Endpoint not specified");
        }
        
        if (string.IsNullOrEmpty(config.LlmApiKey))
        {
            throw new ArgumentException("Llm API Key not specified");
        }
        
        IKernelBuilder builder = Kernel.CreateBuilder();

        builder.AddGoogleAIGeminiChatCompletion(
            modelId: config.LlmModel,
            apiKey: config.LlmApiKey,
            serviceId: config.SemanticKernelServiceId);
        
        
        configureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config);
    }
    
    
    private static void configureLogging(IKernelBuilder builder, CliConfig config)
    {
        if (config.DebugMode)
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            });
        }
    }
}