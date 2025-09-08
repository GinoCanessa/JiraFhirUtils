using System.Text.Json;
using Microsoft.Extensions.Configuration;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;
using jira_fhir_cli.LlmProvider.Providers;

namespace jira_fhir_cli.LlmProvider;

public static class LlmProviderFactory
{
    public static ILlmProvider CreateProvider(LlmConfiguration config)
    {
        return config.ProviderType switch
        {
            LlmProviderType.OpenAI or LlmProviderType.OpenAICompatible => 
                new OpenAICompatibleProvider(config),
            LlmProviderType.Anthropic => 
                throw new NotSupportedException("Anthropic provider not yet implemented"),
            LlmProviderType.Ollama => 
                throw new NotSupportedException("Ollama provider not yet implemented"),
            LlmProviderType.AzureOpenAI => 
                new AzureOpenAIProvider(config),
            LlmProviderType.GoogleAI => 
                throw new NotSupportedException("GoogleAI provider not yet implemented"),
            _ => throw new NotSupportedException($"Provider {config.ProviderType} not supported")
        };
    }
    
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
    
    public static LlmConfiguration CreateDefaultAnthropicConfig(string apiKey, string model = "claude-3-haiku-20240307")
    {
        return new LlmConfiguration
        {
            ProviderType = LlmProviderType.Anthropic,
            ApiEndpoint = "https://api.anthropic.com/v1",
            ApiKey = apiKey,
            Model = model,
            Temperature = 0.3,
            MaxTokens = 500
        };
    }
    
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
}