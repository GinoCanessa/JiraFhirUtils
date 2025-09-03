using JiraFhirUtils.Common;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace jf_loader;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = new()
    {
        ( CliLoadCommand.CommandName, new CliLoadCommand() ),
        ( CliFtsCommand.CommandName, new CliFtsCommand() ),
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
}

public record class CliConfig
{
    public required string DbPath { get; init; }
    public required string JiraXmlDir { get; init; }
    public required bool DropTables { get; init; }
    public required bool KeepCustomFieldSource { get; init; }

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

        // load options that do not require extra processing
        DropTables = pr.GetValue(opt.LoadDropTables);
        KeepCustomFieldSource = pr.GetValue(opt.KeepCustomFieldSource);
    }
}

public class CliLoadCommand : Command
{
    public const string CommandName = "load";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliLoadCommand() : base(CommandName, "Load JIRA issues from XML export files into the database.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.JiraXmlDir);
        this.Add(_cliOptions.LoadDropTables);
        this.Add(_cliOptions.KeepCustomFieldSource);
    }
}

public class CliFtsCommand : Command
{
    public const string CommandName = "fts";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliFtsCommand() : base(CommandName, "Create full-text index tables in the database, using FTS5.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DbPath);
    }
}
