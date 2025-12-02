using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

namespace jira_fhir_cli.Keyword;

public class ScoreFixProcessor
{
    private readonly CliConfig _config;

    public ScoreFixProcessor(CliConfig config)
    {
        _config = config;
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting score fix process...");
        Console.WriteLine($"Using database: {_config.DbPath}");
        Console.WriteLine($"Using BM25 parameters: k1={_config.Bm25K1}, b={_config.Bm25B}");

        using SqliteConnection db = new SqliteConnection($"Data Source={_config.DbPath}");
        await db.OpenAsync();

        try
        {
            // Validate that frequency tables exist
            Console.WriteLine("Validating required frequency data exists...");

            if (!DbCorpusKeywordRecord.ValidateCorpusKeywordsExist(db))
            {
                throw new InvalidOperationException("Corpus keywords table is empty. Run extract-keywords first to populate frequency data.");
            }

            if (!DbIssueKeywordRecord.ValidateIssueKeywordsExist(db))
            {
                throw new InvalidOperationException("Issue keywords table is empty. Run extract-keywords first to populate frequency data.");
            }

            Console.WriteLine("Required frequency data found. Proceeding with score recalculation...");

            // Create BM25 calculator with custom parameters
            Bm25Calculator calculator = new Bm25Calculator(_config.Bm25K1, _config.Bm25B);

            // Call RecalculateAllScores with progress callback
            calculator.RecalculateAllScores(db, message => Console.WriteLine(message));

            Console.WriteLine("Score fix process completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during score fix process: {ex.Message}");
            throw;
        }
    }
}