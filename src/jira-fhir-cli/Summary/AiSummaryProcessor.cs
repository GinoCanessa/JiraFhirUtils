using Microsoft.Data.Sqlite;

namespace jira_fhir_cli.Summary;

public class AiSummaryProcessor
{
    private CliConfig _config;

    public AiSummaryProcessor(CliConfig config)
    {
        _config = config;
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting AI Summarization process...");
        Console.WriteLine($"Using database: {_config.DbPath}");
        

        using SqliteConnection connection = new SqliteConnection($"Data Source={_config.DbPath}");
        await connection.OpenAsync();

    }
}