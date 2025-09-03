using jf_loader.Load;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace jf_loader;

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
                case CliLoadCommand.CommandName:
                    cmd.SetAction(LoadCommandHandler);
                    break;
                case CliFtsCommand.CommandName:
                    cmd.SetAction(FtsCommandHandler);
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
        if (pr.CommandResult.Command is not CliLoadCommand lc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(lc.CommandCliOptions, pr);

        try
        {
            JiraXmlToSql jiraXmlToSql = new(config);
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
        if (pr.CommandResult.Command is not CliFtsCommand fc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }
        CliConfig config = new(fc.CommandCliOptions, pr);
        try
        {
            Processing.JiraFts fts = new(config);
            await fts.ProcessAsync();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating FTS tables: {ex.Message}");
            _retVal = ex.HResult;
        }
    }
}
