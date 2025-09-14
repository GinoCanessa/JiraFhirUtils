using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace jira_fhir_cli;

internal abstract class Program
{
    private static int _retVal = 0;

    // Set up the command line using CliOptions and stub handlers for commands.
    public static async Task<int> Main(string[] args)
    {
        // Build configuration with User Secrets support
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();

        CliOptions cliOptions = new CliOptions();

        RootCommand root = new RootCommand("JIRA FHIR loader CLI");

        // Create commands from CliOptions.Commands so the list is defined in one place
        foreach ((string name, Command cmd) in CliOptions.Commands)
        {
            // set the handlers for each command
            switch (name)
            {
                case CliLoadXmlCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => loadCommandHandler(pr, configuration));
                    break;
                case CliBuildFtsCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => ftsCommandHandler(pr, configuration));
                    break;
                case CliExtractKeywordsCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => keywordCommandHandler(pr, configuration));
                    break;
                case CliSearchBm25Command.CommandName:
                    cmd.SetAction((ParseResult pr) => searchBm25CommandHandler(pr, configuration));
                    break;
                case CliSummarizeCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => summarizeCommandHandler(pr, configuration));
                    break;
                case CliDownloadCommand.CommandName:
                    cmd.SetAction((ParseResult pr) => downloadCommandHandler(pr, configuration));
                    break;
            }

            root.Add(cmd);
        }

        ParseResult pr = root.Parse(args, new ParserConfiguration()
        {
            ResponseFileTokenReplacer = null,
        });

        await pr.InvokeAsync();

        return _retVal;
    }

    private static async Task loadCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliLoadXmlCommand lc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(lc.CommandCliOptions, pr, configuration);

        try
        {
            Load.JiraXmlToSql jiraXmlToSql = new(config);
            await jiraXmlToSql.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing JIRA XML files: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static async Task ftsCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliBuildFtsCommand fc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(fc.CommandCliOptions, pr, configuration);
        try
        {
            FullTextSearch.FtsProcessor fts = new(config);
            await fts.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating FTS tables: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static async Task keywordCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliExtractKeywordsCommand kc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(kc.CommandCliOptions, pr, configuration);
        try
        {
            Keyword.KeywordProcessor kp = new(config);
            await kp.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing keywords: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static async Task searchBm25CommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliSearchBm25Command sc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(sc.CommandCliOptions, pr, configuration);
        try
        {
            Keyword.Bm25SearchProcessor processor = new(config);
            await processor.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error performing BM25 search: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static async Task summarizeCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliSummarizeCommand sc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        
        CliConfig config = new(sc.CommandCliOptions, pr, configuration);
        try
        {
            Summary.AiSummaryProcessor processor = new(config);
            await processor.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating summaries: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static async Task downloadCommandHandler(ParseResult pr, IConfiguration configuration)
    {
        if (pr.CommandResult.Command is not CliDownloadCommand dc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        
        CliConfig config = new(dc.CommandCliOptions, pr, configuration);
        try
        {
            Download.DownloadProcessor processor = new(config);
            await processor.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading JIRA files: {ex.Message}");
            _retVal = ex.HResult;
        }
    }
}
