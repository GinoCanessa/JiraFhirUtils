using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace jira_fhir_cli;

internal class Program
{
    private static int _retVal = 0;

    // Set up the command line using CliOptions and stub handlers for commands.
    public static async Task<int> Main(string[] args)
    {
        CliOptions config = new CliOptions();

        RootCommand root = new RootCommand("JIRA FHIR loader CLI");

        // Create commands from CliOptions.Commands so the list is defined in one place
        foreach ((string name, Command cmd) in CliOptions.Commands)
        {
            // set the handlers for each command
            switch (name)
            {
                case CliLoadXmlCommand.CommandName:
                    cmd.SetAction(LoadCommandHandler);
                    break;
                case CliBuildFtsCommand.CommandName:
                    cmd.SetAction(FtsCommandHandler);
                    break;
                case CliExtractKewordsCommand.CommandName:
                    cmd.SetAction(KeywordCommandHandler);
                    break;
            }

            root.Add(cmd);
        }

        ParseResult pr = root.Parse(args);

        await pr.InvokeAsync();

        return _retVal;
    }

    private static async void LoadCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliLoadXmlCommand lc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(lc.CommandCliOptions, pr);

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

    private static async void FtsCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliBuildFtsCommand fc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(fc.CommandCliOptions, pr);
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

    private static async void KeywordCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliExtractKewordsCommand kc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(kc.CommandCliOptions, pr);
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
}
