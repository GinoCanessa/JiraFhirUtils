using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jira_fhir_cli.FullTextSearch;

internal class FtsProcessor
{
    private CliConfig _config;

    public FtsProcessor(CliConfig config)
    {
        _config = config;
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting JIRA Database Full-text Indexing...");
        Console.WriteLine($"Using database: {_config.DbPath}");

        using SqliteConnection connection = new SqliteConnection($"Data Source={_config.DbPath}");
        await connection.OpenAsync();

        int count;

        Console.WriteLine("Processing issues...");
        IssueFtsRecord.DropTable(connection);
        IssueFtsRecord.CreateTable(connection);
        count = IssueFtsRecord.Populate(connection, sanitizeText: true);
        Console.WriteLine($"  Indexed {count} issues.");
        
        Console.WriteLine("Processing AI summaries...");
        AiIssueFtsRecord.DropTable(connection);
        AiIssueFtsRecord.CreateTable(connection);
        count = AiIssueFtsRecord.Populate(connection, sanitizeText: true);
        Console.WriteLine($"  Indexed {count} AI summaries.");

        Console.WriteLine("Processing comments...");
        CommentFtsRecord.DropTable(connection);
        CommentFtsRecord.CreateTable(connection);
        count = CommentFtsRecord.Populate(connection, sanitizeText: true);
        Console.WriteLine($"  Indexed {count} comments.");

        postProcess(connection);
    }

    private void postProcess(SqliteConnection connection)
    {
        {
            // clean the comments table for comments with no body text
            IDbCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM comments WHERE Body IS NULL OR Body = '';";
            command.ExecuteNonQuery();
        }
    }
}
