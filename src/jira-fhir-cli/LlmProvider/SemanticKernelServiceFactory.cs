using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;
using Microsoft.SemanticKernel.Connectors.Amazon;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;
using System.Text.Json;

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
    public static ISemanticKernelService CreateService(LlmConfiguration config)
    {
        return config.ProviderType switch
        {
            LlmProviderType.OpenAI or LlmProviderType.OpenAICompatible => 
                CreateOpenAIService(config),
            LlmProviderType.AzureOpenAI => 
                CreateAzureOpenAIService(config),
            LlmProviderType.Anthropic => 
                CreateAnthropicService(config),
            LlmProviderType.Ollama => 
                CreateOllamaService(config),
            LlmProviderType.GoogleAI => 
                CreateGoogleAIService(config),
            _ => throw new NotSupportedException($"Provider {config.ProviderType} not supported")
        };
    }
    
    /// <summary>
    /// Creates a SummaryConfiguration from a JSON file, maintaining backward compatibility
    /// </summary>
    /// <param name="configPath">Path to the JSON configuration file</param>
    /// <returns>Loaded summary configuration</returns>
    public static SummaryConfiguration CreateSummaryConfigurationFromFile(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }
        
        try
        {
            string json = File.ReadAllText(configPath);
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            
            SummaryConfiguration? config = JsonSerializer.Deserialize<SummaryConfiguration>(json, options);
            if (config == null)
            {
                throw new InvalidOperationException("Failed to deserialize configuration");
            }
            
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in configuration file: {ex.Message}", ex);
        }
    }

    #region Provider-Specific Factory Methods
    
    private static ISemanticKernelService CreateOpenAIService(LlmConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("API key is required for OpenAI provider");
        }

        IKernelBuilder builder = Kernel.CreateBuilder();
        
        // Add OpenAI chat completion service
        if (config.ProviderType == LlmProviderType.OpenAICompatible)
        {
            // For OpenAI-compatible services (like LMStudio), use custom endpoint
            builder.AddOpenAIChatCompletion(
                modelId: config.Model,
                apiKey: config.ApiKey ?? "not-used", // Some local services don't require real API keys
                endpoint: new Uri(config.ApiEndpoint),
                serviceId: config.SemanticKernelSettings?.ServiceId);
        }
        else
        {
            // For official OpenAI API
            builder.AddOpenAIChatCompletion(
                modelId: config.Model,
                apiKey: config.ApiKey,
                serviceId: config.SemanticKernelSettings?.ServiceId);
        }
        
        // Configure logging if enabled
        ConfigureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        string providerName = config.ProviderType == LlmProviderType.OpenAI ? "OpenAI" : "OpenAI Compatible";
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config, providerName);
    }
    
    private static ISemanticKernelService CreateAzureOpenAIService(LlmConfiguration config)
    {
        string deploymentName = config.DeploymentName ?? 
            config.ProviderSpecificSettings.GetValueOrDefault("DeploymentName", config.Model)?.ToString() ?? 
            config.Model;
            
        IKernelBuilder builder = Kernel.CreateBuilder();
        
        if (config.UseDefaultAzureCredential)
        {
            // Use Azure Default Credential (Managed Identity, etc.)
            TokenCredential credential = new DefaultAzureCredential();
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: config.ApiEndpoint,
                credentials: credential,
                serviceId: config.SemanticKernelSettings?.ServiceId);
        }
        else
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new InvalidOperationException("API key is required for Azure OpenAI provider when not using default credentials");
            }
            
            // Use API key authentication
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: config.ApiEndpoint,
                apiKey: config.ApiKey,
                serviceId: config.SemanticKernelSettings?.ServiceId);
        }
        
        ConfigureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config, "Azure OpenAI");
    }
    
    private static ISemanticKernelService CreateAnthropicService(LlmConfiguration config)
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        
        // Get AWS credentials from provider specific settings or environment
        string? awsAccessKey = config.ProviderSpecificSettings.GetValueOrDefault("AwsAccessKey")?.ToString() 
            ?? config.ApiKey 
            ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            
        string? awsSecretKey = config.ProviderSpecificSettings.GetValueOrDefault("AwsSecretKey")?.ToString()
            ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            
        string awsRegion = config.ProviderSpecificSettings.GetValueOrDefault("AwsRegion")?.ToString() 
            ?? "us-east-1"; // Default to us-east-1 if not specified

        // Add AWS Bedrock chat completion service for Anthropic Claude models
        // The Bedrock connector is experimental, so we need pragma warnings
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only
        builder.AddBedrockChatCompletionService(
            modelId: config.Model,
            serviceId: config.SemanticKernelSettings?.ServiceId);
#pragma warning restore SKEXP0070
        
        ConfigureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config, "Anthropic (AWS Bedrock)");
    }
    
    private static ISemanticKernelService CreateOllamaService(LlmConfiguration config)
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        
        // Add Ollama chat completion service
        // The Ollama connector is in alpha, so we use #pragma warnings in the consuming code
        Uri endpoint = new Uri(config.ApiEndpoint.TrimEnd('/'));
        
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only
        builder.AddOllamaChatCompletion(
            modelId: config.Model,
            endpoint: endpoint,
            serviceId: config.SemanticKernelSettings?.ServiceId);
#pragma warning restore SKEXP0070
        
        ConfigureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config, "Ollama");
    }
    
    private static ISemanticKernelService CreateGoogleAIService(LlmConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("API key is required for Google AI provider");
        }
        
        IKernelBuilder builder = Kernel.CreateBuilder();
        
#pragma warning disable SKEXP0070 // Type is for evaluation purposes only
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: config.Model,
            apiKey: config.ApiKey,
            serviceId: config.SemanticKernelSettings?.ServiceId);
#pragma warning restore SKEXP0070
        
        ConfigureLogging(builder, config);
        
        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        return new SemanticKernelServiceAdapter(kernel, chatService, config, "Google AI");
    }
    
    #endregion

    #region Helper Methods
    
    private static void ConfigureLogging(IKernelBuilder builder, LlmConfiguration config)
    {
        if (config.SemanticKernelSettings?.EnableDetailedLogging == true)
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
            });
        }
    }
    
    #endregion

    #region Backward Compatibility Default Configurations
    
    /// <summary>
    /// Creates a default LMStudio configuration for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultLMStudioConfig()
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.OpenAICompatible,
            ApiEndpoint = "http://localhost:1234/v1",
            Model = "local-model",
            Temperature = 0.3,
            MaxTokens = 500
        };
    }
    
    /// <summary>
    /// Creates a default OpenAI configuration for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultOpenAIConfig(string apiKey, string model = "gpt-4o-mini")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.OpenAI,
            ApiEndpoint = "https://api.openai.com/v1",
            ApiKey = apiKey,
            Model = model,
            Temperature = 0.3,
            MaxTokens = 500
        };
    }
    
    /// <summary>
    /// Creates a default Anthropic configuration for AWS Bedrock for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultAnthropicConfig(string awsAccessKey, string model = "anthropic.claude-3-haiku-20240307-v1:0")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.Anthropic,
            ApiEndpoint = "https://bedrock-runtime.us-east-1.amazonaws.com", // Default endpoint, region can be overridden
            ApiKey = awsAccessKey, // Store as ApiKey for backward compatibility
            Model = model,
            Temperature = 0.3,
            MaxTokens = 500,
            ProviderSpecificSettings = new Dictionary<string, object>
            {
                ["AwsAccessKey"] = awsAccessKey,
                ["AwsRegion"] = "us-east-1"
            }
        };
    }
    
    /// <summary>
    /// Creates a default Ollama configuration for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultOllamaConfig(string model = "llama2", string endpoint = "http://localhost:11434")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.Ollama,
            ApiEndpoint = endpoint,
            Model = model,
            Temperature = 0.3,
            MaxTokens = 500
        };
    }
    
    /// <summary>
    /// Creates a default Azure OpenAI configuration with API key for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultAzureOpenAIConfig(string apiKey, string resourceName, string deploymentName, string model = "gpt-4o-mini")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.AzureOpenAI,
            ApiEndpoint = $"https://{resourceName}.openai.azure.com/",
            ApiKey = apiKey,
            Model = model,
            ApiVersion = "2024-08-01-preview",
            Temperature = 0.3,
            MaxTokens = 500,
            DeploymentName = deploymentName,
            ResourceName = resourceName,
            UseDefaultAzureCredential = false,
            ProviderSpecificSettings = new Dictionary<string, object>
            {
                ["DeploymentName"] = deploymentName,
                ["ResourceName"] = resourceName
            }
        };
    }
    
    /// <summary>
    /// Creates a default Azure OpenAI configuration with managed identity for backward compatibility
    /// </summary>
    public static LlmConfiguration CreateDefaultAzureOpenAIConfigWithManagedIdentity(string resourceName, string deploymentName, string model = "gpt-4o-mini")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.AzureOpenAI,
            ApiEndpoint = $"https://{resourceName}.openai.azure.com/",
            Model = model,
            ApiVersion = "2024-08-01-preview",
            Temperature = 0.3,
            MaxTokens = 500,
            DeploymentName = deploymentName,
            ResourceName = resourceName,
            UseDefaultAzureCredential = true,
            ProviderSpecificSettings = new Dictionary<string, object>
            {
                ["DeploymentName"] = deploymentName,
                ["ResourceName"] = resourceName
            }
        };
    }
    
    /// <summary>
    /// Creates a default summary configuration for backward compatibility
    /// </summary>
    public static SummaryConfiguration CreateDefaultSummaryConfiguration(LlmConfiguration llmConfig)
    {
        return new SummaryConfiguration
        {
            LlmConfig = llmConfig,
            BatchSize = 10,
            OverwriteExistingSummaries = false,
            SummaryTypesToGenerate = SummaryTypes.All,
            Prompts = new PromptTemplates()
        };
    }
    
    #endregion
}