using JiraFhirUtils.Common;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace jira_fhir_cli;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = [
        (CliLoadXmlCommand.CommandName, new CliLoadXmlCommand()),
        (CliBuildFtsCommand.CommandName, new CliBuildFtsCommand()),
        (CliExtractKeywordsCommand.CommandName, new CliExtractKeywordsCommand()),
        (CliSummarizeCommand.CommandName, new CliSummarizeCommand()),
    ];

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

    public Option<string?> LlmConfigPath { get; set; } = new Option<string?>("--llm-config")
    {
        Description = "Path to LLM configuration JSON file.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public Option<string?> LlmProvider { get; set; } = new Option<string?>("--llm-provider")
    {
        Description = "LLM provider (openai, lmstudio, anthropic, ollama).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "lmstudio",
    };

    public Option<string?> LlmApiEndpoint { get; set; } = new Option<string?>("--llm-endpoint")
    {
        Description = "LLM API endpoint URL.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "http://localhost:1234/v1",
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
        DefaultValueFactory = (ar) => "local-model",
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
        DefaultValueFactory = (ar) => 10,
    };
}

public record class CliConfig
{
    public required string DbPath { get; init; }
    public required string JiraXmlDir { get; init; }
    public required bool DropTables { get; init; }
    public required bool KeepCustomFieldSource { get; init; }
    public required string? FhirSpecDatabase { get; init; }
    public required string KeywordDatabase { get; init; }
    public string? LlmConfigPath { get; init; }
    public string? LlmProvider { get; init; }
    public string? LlmApiEndpoint { get; init; }
    public string? LlmModel { get; init; }
    public string? LlmApiVersion { get; init; }
    public bool OverwriteSummaries { get; init; }
    public int BatchSize { get; init; }

    public CliConfig() { }

    [SetsRequiredMembers]
    public CliConfig(CliOptions opt, ParseResult pr)
    {
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

        // load options that do not require extra processing
        DropTables = pr.GetValue(opt.LoadDropTables);
        KeepCustomFieldSource = pr.GetValue(opt.KeepCustomFieldSource);

        // load LLM-related options
        LlmConfigPath = pr.GetValue(opt.LlmConfigPath);
        LlmProvider = pr.GetValue(opt.LlmProvider);
        LlmApiEndpoint = pr.GetValue(opt.LlmApiEndpoint);
        LlmModel = pr.GetValue(opt.LlmModel);
        LlmApiVersion = pr.GetValue(opt.LlmApiVersion);
        OverwriteSummaries = pr.GetValue(opt.OverwriteSummaries);
        BatchSize = pr.GetValue(opt.BatchSize);
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
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.LlmConfigPath);
        this.Add(_cliOptions.LlmProvider);
        this.Add(_cliOptions.LlmApiEndpoint);
        this.Add(_cliOptions.LlmModel);
        this.Add(_cliOptions.LlmApiVersion);
        this.Add(_cliOptions.OverwriteSummaries);
        this.Add(_cliOptions.BatchSize);
    }
}