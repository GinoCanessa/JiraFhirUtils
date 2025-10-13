using JiraFhirUtils.Common;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fmg_r6_review;

public record class CliOptions
{
    public static readonly List<(string, Command)> Commands = new()
    {
        ( CliProcessCommand.CommandName, new CliProcessCommand() ),
        ( CliCreateDictDbCommand.CommandName, new CliCreateDictDbCommand() ),
    };

    public Option<string?> DbPath { get; set; } = new Option<string?>("--db-path")
    {
        Description = "Path to the SQLite database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "fmg_review.sqlite",
    };

    public Option<string?> FhirRepoPath { get; set; } = new Option<string?>("--fhir-repo-path")
    {
        Description = "Path to the local FHIR repository.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };

    public Option<bool> LoadDropTables { get; set; } = new Option<bool>(
        "--drop-tables")
    {
        Description = "Drop existing tables before loading data.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => false,
    };

    public Option<string?> FhirDatabaseCi { get; set; } = new Option<string?>(
        "--fhir-ci-db")
    {
        Description = "Path to the continuous integration (CI) FHIR database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "fhir-r6.sqlite",
    };

    public Option<string?> FhirDatabasePublished { get; set; } = new Option<string?>(
        "--fhir-published-db")
    {
        Description = "Path to the published FHIR database specifications file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "fhir.sqlite",
    };

    public Option<string?> DictionaryDb { get; set; } = new Option<string?>("--dict-db")
    {
        Description = "Path to the SQLite dictionary database file.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "dict.sqlite",
    };

    public Option<string?> DictionarySourcePath { get; set; } = new Option<string?>("--dict-src-path")
    {
        Description = "Path to the dictionary source files.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => "dict",
    };

    public Option<string?> DictionarySourceTgz { get; set; } = new Option<string?>("--dict-src-tgz")
    {
        Description = "Path to a tgz file with dictionary source files.",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = (ar) => null,
    };
}

public record class CliConfig
{
    public required string DbPath { get; init; }
    public required string? FhirRepoPath { get; init; }
    public required bool DropTables { get; init; }
    public required string? FhirDatabaseCi { get; init; }
    public required string? FhirDatabasePublished { get; init; }
    public required string? DictionaryDbPath { get; init; }
    public required string? DictionarySourcePath { get; init; }
    public required string? DictionarySourceTgz { get; init; }

    public CliConfig() { }

    [SetsRequiredMembers]
    public CliConfig(CliOptions opt, ParseResult pr)
    {
        string dbPathParam = pr.GetValue(opt.DbPath) ?? "fmg_review.sqlite";
        string dbPath = FileUtils.FindRelativeFile(null, dbPathParam, false)
            ?? FileUtils.FindRelativeDir(null, dbPathParam, false)
            ?? dbPathParam;

        if (!File.Exists(dbPath) && !Path.IsPathFullyQualified(dbPath))
        {
            dbPath = Path.Combine(Environment.CurrentDirectory, dbPath);
        }

        DbPath = dbPath;

        string? fhirRepoPathParam = pr.GetValue(opt.FhirRepoPath);
        if (!string.IsNullOrEmpty(fhirRepoPathParam))
        {
            string? fhirRepoPath = FileUtils.FindRelativeDir(null, fhirRepoPathParam, false)
                ?? fhirRepoPathParam;
            if (!Directory.Exists(fhirRepoPath) && !Path.IsPathFullyQualified(fhirRepoPath))
            {
                fhirRepoPath = Path.Combine(Environment.CurrentDirectory, fhirRepoPath);
            }
            FhirRepoPath = fhirRepoPath;
        }
        else
        {
            FhirRepoPath = null;
        }

        string? fhirDbCi = pr.GetValue(opt.FhirDatabaseCi);
        if (!string.IsNullOrEmpty(fhirDbCi))
        {
            string? fhirSpecDb = FileUtils.FindRelativeFile(null, fhirDbCi, false)
                ?? fhirDbCi;
            if (!File.Exists(fhirSpecDb) && !Path.IsPathFullyQualified(fhirSpecDb))
            {
                fhirSpecDb = Path.Combine(Environment.CurrentDirectory, fhirSpecDb);
            }
            FhirDatabaseCi = fhirSpecDb;
        }
        else
        {
            FhirDatabaseCi = null;
        }

        string? fhirDbPublished = pr.GetValue(opt.FhirDatabasePublished);
        if (!string.IsNullOrEmpty(fhirDbPublished))
        {
            string? fhirSpecDb = FileUtils.FindRelativeFile(null, fhirDbPublished, false)
                ?? fhirDbPublished;
            if (!File.Exists(fhirSpecDb) && !Path.IsPathFullyQualified(fhirSpecDb))
            {
                fhirSpecDb = Path.Combine(Environment.CurrentDirectory, fhirSpecDb);
            }
            FhirDatabasePublished = fhirSpecDb;
        }
        else
        {
            FhirDatabasePublished = null;
        }

        string? dictDbPathParam = pr.GetValue(opt.DictionaryDb) ?? "dict.sqlite";
        string? dictDbPath = FileUtils.FindRelativeFile(null, dictDbPathParam, false)
            ?? FileUtils.FindRelativeDir(null, dictDbPathParam, false)
            ?? dictDbPathParam;
        if (!string.IsNullOrEmpty(dictDbPath))
        {
            if (!File.Exists(dictDbPath) && !Path.IsPathFullyQualified(dictDbPath))
            {
                dictDbPath = Path.Combine(Environment.CurrentDirectory, dictDbPath);
            }
            DictionaryDbPath = dictDbPath;
        }
        else
        {
            DictionaryDbPath = null;
        }

        string? dictSrcPathParam = pr.GetValue(opt.DictionarySourcePath) ?? "dict";
        string? dictSrcPath = FileUtils.FindRelativeDir(null, dictSrcPathParam, false)
            ?? dictSrcPathParam;
        if (!string.IsNullOrEmpty(dictSrcPath))
        {
            if (!Directory.Exists(dictSrcPath) && !Path.IsPathFullyQualified(dictSrcPath))
            {
                dictSrcPath = Path.Combine(Environment.CurrentDirectory, dictSrcPath);
            }
            DictionarySourcePath = dictSrcPath;
        }
        else
        {
            DictionarySourcePath = null;
        }

        string? dictSrcTgz = pr.GetValue(opt.DictionarySourceTgz);
        if (!string.IsNullOrEmpty(dictSrcTgz))
        {
            string? dictTgzPath = FileUtils.FindRelativeFile(null, dictSrcTgz, false)
                ?? dictSrcTgz;
            if (!File.Exists(dictTgzPath) && !Path.IsPathFullyQualified(dictTgzPath))
            {
                dictTgzPath = Path.Combine(Environment.CurrentDirectory, dictTgzPath);
            }
            DictionarySourceTgz = dictTgzPath;
        }
        else
        {
            DictionarySourceTgz = null;
        }

        // load options that do not require extra processing
        DropTables = pr.GetValue(opt.LoadDropTables);
    }
}

public class CliProcessCommand : Command
{
    public const string CommandName = "process";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliProcessCommand() : base(CommandName, "Process the current FHIR build for review.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DbPath);
        this.Add(_cliOptions.FhirRepoPath);
        this.Add(_cliOptions.FhirDatabaseCi);
        this.Add(_cliOptions.FhirDatabasePublished);
        this.Add(_cliOptions.DictionaryDb);
        this.Add(_cliOptions.LoadDropTables);
    }
}

public class CliCreateDictDbCommand : Command
{
    public const string CommandName = "create-dict-db";

    private CliOptions _cliOptions = new();
    public CliOptions CommandCliOptions => _cliOptions;

    public CliCreateDictDbCommand() : base(CommandName, "Create the dictionary database from source files.")
    {
        // Add options defined in CliOptions
        this.Add(_cliOptions.DictionaryDb);
        this.Add(_cliOptions.DictionarySourcePath);
        this.Add(_cliOptions.DictionarySourceTgz);
    }
}
