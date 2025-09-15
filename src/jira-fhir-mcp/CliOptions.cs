using JiraFhirUtils.Common;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace jira_fhir_mcp;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = [
        (CliMcpHttpXmlCommand.CommandName, new CliMcpHttpXmlCommand()),
    ];

    public Option<bool> DebugMode { get; set; } = new Option<bool>(
        "--debug")
    {
        Description = "Enable debug mode with verbose logging.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<int> Port { get; set; } = new Option<int>(name: "--port", aliases: "-p")
    {
        Description = "Port number for the MCP HTTP server.",
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = (ar) => 5000,
    };
    
    public Option<string?> PublicUrl { get; set; } = new Option<string?>(
        "--url")
    {
        Description = "Public URL for accessing the MCP server.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };
    
    public Option<string?> DbPath { get; set; } = new Option<string?>("--db-path")
    {
        Description = "Path to the SQLite database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "jira_issues.sqlite",
    };

    public Option<string?> FhirSpecDatabase { get; set; } = new Option<string?>(
        "--fhir-spec-database")
    {
        Description = "Path to the FHIR specification database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<string?> LlmProvider { get; set; } = new Option<string?>("--llm-provider")
    {
        Description = "LLM provider (openai, openrouter, lmstudio, azureopenai). Default: openai-compatible.",
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = (ar) => null,
    };
    
    public Option<string?> LlmApiKey { get; set; } = new Option<string?>("--llm-api-key")
    {
        Description = "LLM API key. If not provided, will look for environment variable or user secret.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<string?> LlmApiEndpoint { get; set; } = new Option<string?>("--llm-endpoint")
    {
        Description = "LLM API endpoint URL.",
        Arity = ArgumentArity.ExactlyOne,
        DefaultValueFactory = (ar) => null,
    };
    
    public Option<string?> LlmApiVersion { get; set; } = new Option<string?>("--llm-api-version")
    {
        Description = "LLM API version (if applicable).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<string?> LlmModel { get; set; } = new Option<string?>("--llm-model")
    {
        Description = "Model name to use for summaries.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };
    
    public Option<double> LlmTemperature { get; set; } = new Option<double>("--llm-temperature")
    {
        Description = "Temperature setting for LLM (0.0 to 1.0).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 0.3,
    };
    
    public Option<int> LlmMaxTokens { get; set; } = new Option<int>("--llm-max-tokens")
    {
        Description = "Maximum tokens for LLM response.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 1000,
    };
    
    public Option<string?> LlmDeploymentName { get; set; } = new Option<string?>("--llm-deployment-name")
    {
        Description = "Deployment name for Azure OpenAI (if applicable).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };
    
    public Option<string?> LlmResourceName { get; set; } = new Option<string?>("--llm-resource-name")
    {
        Description = "Resource name for Azure OpenAI (if applicable).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };
}

public record class CliConfig
{
    public bool DebugMode { get; init; } = false;
    public required int Port { get; init; }
    public required string PublicUrl { get; init; }
    public required string DbPath { get; init; }
    public required string? FhirSpecDatabase { get; init; }
    public string? LlmProvider { get; init; }
    public string? LlmApiEndpoint { get; init; }
    public string? LlmApiKey { get; init; }
    public string? LlmModel { get; init; }
    public string? LlmApiVersion { get; init; }
    public double LlmTemperature { get; init; }
    public int LlmMaxTokens { get; init; }
    public string? LlmDeploymentName { get; init; }
    public string? LlmResourceName { get; init; }
    public IConfiguration Configuration { get; init; }

    public CliConfig() { Configuration = null!; }

    [SetsRequiredMembers]
    public CliConfig(CliOptions opt, ParseResult pr, IConfiguration configuration)
    {
        Configuration = configuration;
        string dbPathParam = pr.GetValue(opt.DbPath) ?? "jira_issues.sqlite";
        string dbPath = FileUtils.FindRelativeFile(null, dbPathParam, false)
            ?? FileUtils.FindRelativeDir(null, dbPathParam, false)
            ?? dbPathParam;

        if (!File.Exists(dbPath) && !Path.IsPathFullyQualified(dbPath))
        {
            dbPath = Path.Combine(Environment.CurrentDirectory, dbPath);
        }

        DbPath = dbPath;

        string? fhirSpecDbParam = pr.GetValue(opt.FhirSpecDatabase);
        if (!string.IsNullOrEmpty(fhirSpecDbParam))
        {
            string? fhirSpecDb = FileUtils.FindRelativeFile(null, fhirSpecDbParam, false)
                ?? fhirSpecDbParam;
            if (!File.Exists(fhirSpecDb) && !Path.IsPathFullyQualified(fhirSpecDb))
            {
                fhirSpecDb = Path.Combine(Environment.CurrentDirectory, fhirSpecDb);
            }
            FhirSpecDatabase = fhirSpecDb;
        }
        else
        {
            FhirSpecDatabase = null;
        }

        // load options that do not require extra processing
        DebugMode = pr.GetValue(opt.DebugMode);
        Port = pr.GetValue(opt.Port);
        PublicUrl = pr.GetValue(opt.PublicUrl) ?? $"http://localhost:{Port}";

        // load LLM-related options with configuration hierarchy
        LlmProvider = pr.GetValue(opt.LlmProvider);
        LlmApiEndpoint = pr.GetValue(opt.LlmApiEndpoint) ?? getDefaultApiEndpoint(LlmProvider);
        LlmModel = pr.GetValue(opt.LlmModel);
        LlmApiVersion = pr.GetValue(opt.LlmApiVersion);
        LlmTemperature = pr.GetValue(opt.LlmTemperature);
        LlmMaxTokens = pr.GetValue(opt.LlmMaxTokens);
        LlmDeploymentName = pr.GetValue(opt.LlmDeploymentName);
        LlmResourceName = pr.GetValue(opt.LlmResourceName);
        LlmApiKey = pr.GetValue(opt.LlmApiKey) ?? getApiKey(LlmProvider ?? string.Empty);
    }

    private string? getDefaultApiEndpoint(string? provider) => provider?.ToLowerInvariant() switch
    {
        "openai" => "https://api.openai.com/api/v1",
        "openrouter" => "https://openrouter.ai/api/v1",
        "azure" => null,
        "azureopenai" => null, // Azure OpenAI endpoints are resource-specific
        "lmstudio" => "http://localhost:1234/v1",
        "ollama" => "http://localhost:11434/api/v1",
        _ => "https://openrouter.ai/api/v1"
    };

    /// <summary>
    /// Get configuration value with hierarchical lookup: User Secrets -> Environment Variables -> Default
    /// </summary>
    /// <param name="key">The configuration key to look up</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>Configuration value or default</returns>
    private string? getConfigurationValue(string key, string? defaultValue = null)
    {
        return Configuration[key] ?? defaultValue;
    }
    
    /// <summary>
    /// Get API key for a specific LLM provider with hierarchical lookup
    /// </summary>
    /// <param name="provider">Provider name (openai, anthropic, azureopenai, etc.)</param>
    /// <returns>API key or null</returns>
    private string? getApiKey(string provider)
    {
        // Try provider-specific key first, then generic API key, then common environment variable names
        return getConfigurationValue($"LLM:{provider}:ApiKey") 
               ?? getConfigurationValue($"{provider}:ApiKey") 
               ?? getConfigurationValue("LLM:ApiKey")
               ?? getConfigurationValue(provider.ToUpperInvariant() + "_API_KEY")
               ?? (provider.ToLowerInvariant() switch
               {
                   "azureopenai" => getConfigurationValue("AZURE_OPENAI_API_KEY"),
                   _ => null
               });
    }
}

public class CliMcpHttpXmlCommand : Command
{
    public const string CommandName = "mcp-http";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliMcpHttpXmlCommand() : base(CommandName, "Run the MCP server and serve via HTTP.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.Port);
        this.Add(_cliOptions.PublicUrl);
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.FhirSpecDatabase);
    }
}
