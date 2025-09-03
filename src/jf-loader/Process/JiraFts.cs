using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jf_loader.Process;

internal class JiraFts
{
    private CliConfig _config;

    public JiraFts(CliConfig config)
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

        Console.WriteLine("Processing comments...");
        CommentFtsRecord.DropTable(connection);
        CommentFtsRecord.CreateTable(connection);
        count = CommentFtsRecord.Populate(connection, sanitizeText: true);
        Console.WriteLine($"  Indexed {count} comments.");
    }
}
