using JiraFhirUtils.Common;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace jira_fhir_cli;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = [
        (CliDownloadCommand.CommandName, new CliDownloadCommand()),
        (CliLoadXmlCommand.CommandName, new CliLoadXmlCommand()),
        (CliBuildFtsCommand.CommandName, new CliBuildFtsCommand()),
        (CliExtractKeywordsCommand.CommandName, new CliExtractKeywordsCommand()),
        (CliSearchBm25Command.CommandName, new CliSearchBm25Command()),
        (CliSummarizeCommand.CommandName, new CliSummarizeCommand()),
        (CliFixScoresCommand.CommandName, new CliFixScoresCommand()),
    ];

    public Option<bool> DebugMode { get; set; } = new Option<bool>(
        "--debug")
    {
        Description = "Enable debug mode with verbose logging.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };
    
    public Option<string?> DbPath { get; set; } = new Option<string?>("--db-path")
    {
        Description = "Path to the SQLite database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "jira_issues.sqlite",
    };

    public Option<string?> JiraXmlDir { get; set; } = new Option<string?>(
        "--jira-xml-dir",
        "--initial-dir")
    {
        Description = "Path to the directory containing JIRA XML export files.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "bulk",
    };

    public Option<bool> LoadDropTables { get; set; } = new Option<bool>(
        "--drop-tables")
    {
        Description = "Drop existing tables before loading data.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<bool> KeepCustomFieldSource { get; set; } = new Option<bool>(
        "--keep-custom-field-source")
    {
        Description = "Keep the source table for custom fields in the database.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<string?> FhirSpecDatabase { get; set; } = new Option<string?>(
        "--fhir-spec-database")
    {
        Description = "Path to the FHIR specification database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<string> KeywordDatabase { get; set; } = new Option<string>(
        "--keyword-database")
    {
        Description = "Path to a SQLite database with auxiliary data for processing keywords.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "auxiliary.sqlite",
    };

    public Option<string?> JiraSpecification { get; set; } = new Option<string?>(
        "--specification",
        "--spec")
    {
        Description = "Optional JIRA specification filter.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<string> JiraCookie { get; set; } = new Option<string>(
        "--jira-cookie",
        "--cookie")
    {
        Description = "JIRA authentication cookie (required).",
        Arity = ArgumentArity.ExactlyOne,
    };
    
    public Option<int?> DownloadLimit { get; set; } = new Option<int?>(
        "--download-limit")
    {
        Description = "Optional limit on number of days to download.",
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
    

    public Option<bool> OverwriteSummaries { get; set; } = new Option<bool>("--overwrite")
    {
        Description = "Overwrite existing AI summaries.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<int> BatchSize { get; set; } = new Option<int>("--batch-size")
    {
        Description = "Number of issues to process in each batch.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 100,
    };

    public Option<string?> SearchQuery { get; set; } = new Option<string?>("--query")
    {
        Description = "Search query for BM25 ranking.",
        Arity = ArgumentArity.ExactlyOne,
    };

    public Option<int> SearchTopK { get; set; } = new Option<int>("--top-k", "--limit")
    {
        Description = "Number of top results to return.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 20,
    };

    public Option<string?> SearchKeywordType { get; set; } = new Option<string?>("--keyword-type")
    {
        Description = "Filter results by keyword type (Word, FhirElementPath, FhirOperationName).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<double> Bm25K1 { get; set; } = new Option<double>("--bm25-k1")
    {
        Description = "BM25 k1 parameter (term frequency saturation).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 1.2,
    };

    public Option<double> Bm25B { get; set; } = new Option<double>("--bm25-b")
    {
        Description = "BM25 b parameter (length normalization).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => 0.75,
    };

    public Option<bool> ShowTopKeywords { get; set; } = new Option<bool>("--show-keywords")
    {
        Description = "Show top keywords by IDF score.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<bool> ShowProgress { get; set; } = new Option<bool>("--show-progress")
    {
        Description = "Show progress during score recalculation.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => true,
    };
}

public record class CliConfig
{
    public bool DebugMode { get; init; } = false;
    public required string DbPath { get; init; }
    public required string JiraXmlDir { get; init; }
    public required bool DropTables { get; init; }
    public required bool KeepCustomFieldSource { get; init; }
    public required string? FhirSpecDatabase { get; init; }
    public required string KeywordDatabase { get; init; }
    public string? JiraSpecification { get; init; }
    public required string JiraCookie { get; init; }
    public int? DownloadLimit { get; init; }
    public string? LlmProvider { get; init; }
    public string? LlmApiEndpoint { get; init; }
    public string? LlmApiKey { get; init; }
    public string? LlmModel { get; init; }
    public string? LlmApiVersion { get; init; }
    public double LlmTemperature { get; init; }
    public int LlmMaxTokens { get; init; }
    public string? LlmDeploymentName { get; init; }
    public string? LlmResourceName { get; init; }
    public bool OverwriteSummaries { get; init; }
    public int BatchSize { get; init; }
    public string? SearchQuery { get; init; }
    public int SearchTopK { get; init; }
    public string? SearchKeywordType { get; init; }
    public double Bm25K1 { get; init; }
    public double Bm25B { get; init; }
    public bool ShowTopKeywords { get; init; }
    public bool ShowProgress { get; init; }
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


        string jiraXmlDirParam = pr.GetValue(opt.JiraXmlDir) ?? "bulk";
        string jiraXmlDir = FileUtils.FindRelativeDir(null, jiraXmlDirParam, false)
            ?? jiraXmlDirParam;

        if (!Directory.Exists(jiraXmlDir) && !Path.IsPathFullyQualified(jiraXmlDir))
        {
            jiraXmlDir = Path.Combine(Environment.CurrentDirectory, jiraXmlDir);
        }

        JiraXmlDir = jiraXmlDir;

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

        string keywordDbFileParam = pr.GetValue(opt.KeywordDatabase) ?? "./Data/StopWords.txt";
        string keywordDbFile = FileUtils.FindRelativeFile(null, keywordDbFileParam, false)
            ?? keywordDbFileParam;
        if (!File.Exists(keywordDbFile) && !Path.IsPathFullyQualified(keywordDbFile))
        {
            keywordDbFile = Path.Combine(Environment.CurrentDirectory, keywordDbFile);
        }

        KeywordDatabase = keywordDbFile;

        // load new download-related options
        JiraSpecification = pr.GetValue(opt.JiraSpecification);
        JiraCookie = pr.GetValue(opt.JiraCookie) ?? string.Empty;   // throw new ArgumentException("JiraCookie is required");
        DownloadLimit = pr.GetValue(opt.DownloadLimit);

        // load options that do not require extra processing
        DropTables = pr.GetValue(opt.LoadDropTables);
        KeepCustomFieldSource = pr.GetValue(opt.KeepCustomFieldSource);
        DebugMode = pr.GetValue(opt.DebugMode);

        // load LLM-related options with configuration hierarchy
        LlmProvider = pr.GetValue(opt.LlmProvider);
        LlmApiEndpoint = pr.GetValue(opt.LlmApiEndpoint) ?? getDefaultApiEndpoint(LlmProvider);
        LlmModel = pr.GetValue(opt.LlmModel);
        LlmApiVersion = pr.GetValue(opt.LlmApiVersion);
        OverwriteSummaries = pr.GetValue(opt.OverwriteSummaries);
        BatchSize = pr.GetValue(opt.BatchSize);
        LlmTemperature = pr.GetValue(opt.LlmTemperature);
        LlmMaxTokens = pr.GetValue(opt.LlmMaxTokens);
        LlmDeploymentName = pr.GetValue(opt.LlmDeploymentName);
        LlmResourceName = pr.GetValue(opt.LlmResourceName);
        LlmApiKey = pr.GetValue(opt.LlmApiKey) ?? getApiKey(LlmProvider ?? string.Empty);

        // load BM25 search options
        SearchQuery = pr.GetValue(opt.SearchQuery);
        SearchTopK = pr.GetValue(opt.SearchTopK);
        SearchKeywordType = pr.GetValue(opt.SearchKeywordType);
        Bm25K1 = pr.GetValue(opt.Bm25K1);
        Bm25B = pr.GetValue(opt.Bm25B);
        ShowTopKeywords = pr.GetValue(opt.ShowTopKeywords);
        ShowProgress = pr.GetValue(opt.ShowProgress);
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

public class CliLoadXmlCommand : Command
{
    public const string CommandName = "load-xml";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliLoadXmlCommand() : base(CommandName, "Load JIRA issues from XML export files into the database.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.JiraXmlDir);
        this.Add(_cliOptions.LoadDropTables);
        this.Add(_cliOptions.KeepCustomFieldSource);
    }
}

public class CliBuildFtsCommand : Command
{
    public const string CommandName = "build-fts";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliBuildFtsCommand() : base(CommandName, "Create full-text index tables in the database, using FTS5.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.DbPath);
    }
}

public class CliExtractKeywordsCommand : Command
{
    public const string CommandName = "extract-keywords";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;
    public CliExtractKeywordsCommand() : base(CommandName, "Extract and display keywords from the issues in the database.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.FhirSpecDatabase);
        this.Add(_cliOptions.KeywordDatabase);
    }
}

public class CliSummarizeCommand : Command
{
    public const string CommandName = "summarize";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;
    public CliSummarizeCommand() : base(CommandName, "Create summaries of issues, comments, and resolutions.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.LlmProvider);
        this.Add(_cliOptions.LlmApiKey);
        this.Add(_cliOptions.LlmApiEndpoint);
        this.Add(_cliOptions.LlmModel);
        this.Add(_cliOptions.LlmApiVersion);
        this.Add(_cliOptions.LlmTemperature);
        this.Add(_cliOptions.LlmMaxTokens);
        this.Add(_cliOptions.LlmDeploymentName);
        this.Add(_cliOptions.LlmResourceName);
        this.Add(_cliOptions.OverwriteSummaries);
        this.Add(_cliOptions.BatchSize);
    }
}

public class CliDownloadCommand : Command
{
    public const string CommandName = "download";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;
    public CliDownloadCommand() : base(CommandName, "Download JIRA XML files by week starting from current week working backwards")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.JiraSpecification);
        this.Add(_cliOptions.JiraCookie);
        this.Add(_cliOptions.JiraXmlDir);
        this.Add(_cliOptions.DownloadLimit);
    }
}

public class CliSearchBm25Command : Command
{
    public const string CommandName = "search-bm25";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;
    public CliSearchBm25Command() : base(CommandName, "Search issues using BM25 ranking algorithm.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DebugMode);
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.FhirSpecDatabase);
        this.Add(_cliOptions.KeywordDatabase);
        this.Add(_cliOptions.SearchQuery);
        this.Add(_cliOptions.SearchTopK);
        this.Add(_cliOptions.SearchKeywordType);
        this.Add(_cliOptions.Bm25K1);
        this.Add(_cliOptions.Bm25B);
        this.Add(_cliOptions.ShowTopKeywords);
    }
}

public class CliFixScoresCommand : Command
{
    public const string CommandName = "fix-scores";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliFixScoresCommand() : base(CommandName, "Recalculate IDF and BM25 scores using existing frequency counts")
    {
        // Add required options
        this.Add(_cliOptions.DbPath);

        // Add optional options
        this.Add(_cliOptions.Bm25K1);
        this.Add(_cliOptions.Bm25B);
        this.Add(_cliOptions.ShowProgress);
    }
}