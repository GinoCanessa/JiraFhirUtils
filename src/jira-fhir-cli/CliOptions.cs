using JiraFhirUtils.Common;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace jira_fhir_cli;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = new()
    {
        ( CliLoadXmlCommand.CommandName, new CliLoadXmlCommand() ),
        ( CliBuildFtsCommand.CommandName, new CliBuildFtsCommand() ),
        ( CliExtractKewordsCommand.CommandName, new CliExtractKewordsCommand() ),
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

    public Option<string> StopwordFile { get; set; } = new Option<string>(
        "--stopword-file")
    {
        Description = "Path to a file containing stopwords for full-text search indexing.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "./Data/StopWords.txt",
    };
}

public record class CliConfig
{
    public required string DbPath { get; init; }
    public required string JiraXmlDir { get; init; }
    public required bool DropTables { get; init; }
    public required bool KeepCustomFieldSource { get; init; }
    public required string? FhirSpecDatabase { get; init; }
    public required string StopwordFile { get; init; }

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

        string stopwordFileParam = pr.GetValue(opt.StopwordFile) ?? "./Data/StopWords.txt";
        string stopwordFile = FileUtils.FindRelativeFile(null, stopwordFileParam, false)
            ?? stopwordFileParam;
        if (!File.Exists(stopwordFile) && !Path.IsPathFullyQualified(stopwordFile))
        {
            stopwordFile = Path.Combine(Environment.CurrentDirectory, stopwordFile);
        }

        StopwordFile = stopwordFile;

        // load options that do not require extra processing
        DropTables = pr.GetValue(opt.LoadDropTables);
        KeepCustomFieldSource = pr.GetValue(opt.KeepCustomFieldSource);
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

public class CliExtractKewordsCommand : Command
{
    public const string CommandName = "extract-keywords";
    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;
    public CliExtractKewordsCommand() : base(CommandName, "Extract and display keywords from the issues in the database.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.FhirSpecDatabase);
        this.Add(_cliOptions.StopwordFile);
    }
}
