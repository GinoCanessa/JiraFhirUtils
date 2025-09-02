using System.CommandLine;
using System.CommandLine.Parsing;

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
                case "load":
                    cmd.SetAction(LoadCommandHandler);
                    break;
            }

            root.Add(cmd);
        }

        ParseResult pr = root.Parse(args);

        await pr.InvokeAsync();

        return _retVal;
    }

    private static void LoadCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliLoadCommand lc)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            return;
        }

        CliConfig config = new(lc.CommandCliOptions, pr);

        Console.WriteLine("load command invoked");
        Console.WriteLine($"  --db-path: {config.DbPath}");
        Console.WriteLine($"  --jira-xml-dir: {config.JiraXmlDir}");

        // set our program return value
        _retVal = 0;
    }
}
