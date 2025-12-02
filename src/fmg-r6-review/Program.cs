using fmg_r6_review.Dict;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

namespace fmg_r6_review;

internal class Program
{
    private static int _retVal = 0;

    // Set up the command line using CliOptions and stub handlers for commands.
    public static async Task<int> Main(string[] args)
    {
        CliOptions config = new CliOptions();

        RootCommand root = new RootCommand("FHIR FMG R6 Prep Tool");

        // Create commands from CliOptions.Commands so the list is defined in one place
        foreach ((string name, Command cmd) in CliOptions.Commands)
        {
            // set the handlers for each command
            switch (name)
            {
                case CliCreateDictDbCommand.CommandName:
                    cmd.SetAction(CreateDictCommandHandler);
                    break;
                case CliProcessCommand.CommandName:
                    cmd.SetAction(ProcessCommandHandler);
                    break;
                case CliGenerateCommand.CommandName:
                    cmd.SetAction(GenerateCommandHandler);
                    break;
            }

            root.Add(cmd);
        }

        ParseResult pr = root.Parse(args);

        await pr.InvokeAsync();

        return _retVal;
    }

    private static void GenerateCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliGenerateCommand genCommand)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(genCommand.CommandCliOptions, pr);

        try
        {
            Generate.ConfluenceGenerator generator = new(config);
            generator.Generate();

            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing JIRA XML files: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static void ProcessCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliProcessCommand processCommand)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(processCommand.CommandCliOptions, pr);

        try
        {
            // make sure our workgroups are loaded
            SpecReview.WorkGroupLoader wgLoader = new(config);
            wgLoader.LoadWorkGroups();
            wgLoader.LoadFmgFeedbackSheetContent();

            // process the pages
            SpecReview.ContentReview reviewProcessor = new(config);

            reviewProcessor.ProcessPages();
            reviewProcessor.ProcessArtifacts();

            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing JIRA XML files: {ex.Message}");
            _retVal = ex.HResult;
        }
    }

    private static void CreateDictCommandHandler(ParseResult pr)
    {
        if (pr.CommandResult.Command is not CliCreateDictDbCommand createDictCommand)
        {
            Console.WriteLine("Incorrect mapping from command to command handler!");
            _retVal = 1;
            return;
        }

        CliConfig config = new(createDictCommand.CommandCliOptions, pr);

        try
        {
            DictLoader loader = new(config);
            loader.LoadDictionary();
            _retVal = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing JIRA XML files: {ex.Message}");
            _retVal = ex.HResult;
        }
    }
}
