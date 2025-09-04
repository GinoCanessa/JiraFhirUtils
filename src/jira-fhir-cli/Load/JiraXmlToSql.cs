using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System.CommandLine;
using System.Globalization;
using System.Transactions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace jira_fhir_cli.Load;

internal class JiraXmlToSql
{
    // Configuration constants
    private const string XmlFilePattern = "*.xml";

    private CliConfig _config;

    public JiraXmlToSql(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Processes JIRA XML export files and loads them into a SQLite database.
    /// </summary>
    /// <param name="config">CLI configuration containing database path and XML directory</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting JIRA XML to SQLite import process...");

        Console.WriteLine($"Using database: {_config.DbPath}");
        Console.WriteLine($"JIRA Xml directory: {_config.JiraXmlDir}");
        
        // Guard: Check if initial directory exists
        if (!Directory.Exists(_config.JiraXmlDir))
        {
            Console.WriteLine($"Error: JIRA XML directory '{_config.JiraXmlDir}' not found.");
            return;
        }
        
        using SqliteConnection connection = new SqliteConnection($"Data Source={_config.DbPath}");
        await connection.OpenAsync();

        // drop tables first, if requested
        if (_config.DropTables)
        {
            DropTables(connection);
        }

        // create the tables
        CreateTablesAndIndexes(connection);

        // check for XML files we need to process
        List<string> xmlFiles = Directory.EnumerateFiles(_config.JiraXmlDir, XmlFilePattern, SearchOption.AllDirectories).ToList();

        if (xmlFiles.Count == 0)
        {
            Console.WriteLine($"No XML files found matching pattern '{XmlFilePattern}' in directory '{_config.JiraXmlDir}'.");
            return;
        }
        
        Console.WriteLine($"Found {xmlFiles.Count} XML files to process.");

        // need to process in DESCENDING order so that the latest updates are processed last
        foreach (string filePath in xmlFiles.OrderByDescending(f => f))
        {
            ProcessXmlFile(filePath, connection);
        }
        
        // Migrate custom field data from custom_fields table to issueRecords table
        await UpdateIssuesFromCustomFieldsAsync(connection);
        
        // we no longer need the custom fields table
        if (!_config.KeepCustomFieldSource)
        {
            CustomFieldRecord.DropTable(connection);
        }

        Console.WriteLine("\nImport process finished.");
    }

    private void DropTables(SqliteConnection connection)
    {
        Console.WriteLine("Dropping existing tables...");

        // Disable FKs so we can drop in any order
        using (SqliteCommand fkOff = connection.CreateCommand())
        {
            fkOff.CommandText = "PRAGMA foreign_keys = OFF;";
            fkOff.ExecuteNonQuery();
        }

        using SqliteTransaction tx = connection.BeginTransaction();

        try
        {
            // Get all user tables (exclude internal sqlite_* tables)
            List<string> tableNames = new();

            using (SqliteCommand listCmd = connection.CreateCommand())
            {
                listCmd.Transaction = tx;
                listCmd.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                using SqliteDataReader reader = listCmd.ExecuteReader();
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            if (tableNames.Count == 0)
            {
                Console.WriteLine("No user tables found.");
            }
            else
            {
                using SqliteCommand dropCmd = connection.CreateCommand();
                dropCmd.Transaction = tx;

                foreach (string table in tableNames)
                {
                    string quoted = "\"" + table.Replace("\"", "\"\"") + "\""; // safe quote
                    dropCmd.CommandText = $"DROP TABLE IF EXISTS {quoted};";
                    dropCmd.ExecuteNonQuery();
                    Console.WriteLine($"✓ Dropped table: {table}");
                }
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to drop tables: {ex.Message}");
            tx.Rollback();
            throw;
        }
        finally
        {
            using SqliteCommand fkOn = connection.CreateCommand();
            fkOn.CommandText = "PRAGMA foreign_keys = ON;";
            fkOn.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Creates the known tables.
    /// </summary>
    /// <param name="connection">The database connection</param>
    private void CreateTablesAndIndexes(SqliteConnection connection)
    {
        Console.WriteLine("Initializing database...");
        
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText = "PRAGMA journal_mode = WAL;"; // for better performance and concurrency
        command.ExecuteNonQuery();

        IssueRecord.CreateTable(connection);
        IssueRecord.LoadMaxKey(connection);

        CustomFieldRecord.CreateTable(connection);
        CustomFieldRecord.LoadMaxKey(connection);

        CommentRecord.CreateTable(connection);
        CommentRecord.LoadMaxKey(connection);

        Console.WriteLine("Database schema is ready.");
    }


    /// <summary>
    /// Processes a single XML file and loads its data into the database.
    /// </summary>
    /// <param name="filePath">The path to the XML file</param>
    /// <param name="connection">The database connection</param>
    private void ProcessXmlFile(string filePath, SqliteConnection connection)
    {
        Console.WriteLine($"\nProcessing file: {filePath}");

        try
        {
            // Load and validate JIRA data from the XML file
            List<JiraItem> items;
            try
            {
                items = ParseXmlJiraItems(filePath);
            }
            catch (InvalidOperationException)
            {
                // Handle empty file case - already logged in ParseXmlJiraItems
                Console.WriteLine($"No items found in {filePath}.");
                return;
            }

            // split the JiraItems into the respective records
            LoadItemsIntoDb(items, connection, filePath);
        }
        catch (Exception error)
        {
            Console.WriteLine($"Failed to process file {filePath}: {error.Message}");
        }
    }

    /// <summary>
    /// Loads and validates JIRA data from an XML file.
    /// </summary>
    /// <param name="filePath">The path to the XML file</param>
    /// <returns>List of JIRA items</returns>
    /// <exception cref="InvalidOperationException">Thrown when no items are found in the file</exception>
    private List<JiraItem> ParseXmlJiraItems(string filePath)
    {
        JiraRss jiraData = JiraXmlHelper.DeserializeFromFile(filePath);
        List<JiraItem> items = jiraData.Channel.Items;
        
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"No items found in {filePath}.");
        }

        return items;
    }

    private int LoadItemsIntoDb(
        List<JiraItem> items,
        SqliteConnection connection,
        string filePath)
    {
        List<IssueRecord> issueRecords = [];
        List<CustomFieldRecord> customFieldRecords = [];
        List<CommentRecord> commentRecords = [];

        foreach (JiraItem item in items)
        {
            string? issueKey = item.Key.Value;
            if (string.IsNullOrWhiteSpace(issueKey))
            {
                Console.WriteLine($"Skipping item with missing key in {filePath}: {item.Title ?? "Unknown Title"}");
                continue;
            }

            IssueRecord issueRecord = item.ToIssueRecord(issueKey);
            issueRecords.Add(issueRecord);

            foreach (JiraXmlCustomField field in (item.CustomFields?.CustomFieldList ?? []))
            {
                customFieldRecords.Add(field.ToCustomFieldRecord(issueRecord));
            }

            foreach (JiraComment comment in item.Comments?.CommentList ?? [])
            {
                // Convert to database record using extension method
                CommentRecord commentRec = comment.ToCommentRecord(issueRecord);
                if (string.IsNullOrEmpty(commentRec.Body))
                {
                    continue;
                }
                commentRecords.Add(commentRec);
            }
        }

        issueRecords.Insert(connection, ignoreDuplicates: true, insertPrimaryKey: true);
        customFieldRecords.Insert(connection, ignoreDuplicates: true, insertPrimaryKey: true);
        commentRecords.Insert(connection, ignoreDuplicates: true, insertPrimaryKey: true);

        Console.WriteLine($"Successfully processed {filePath}:");
        Console.WriteLine($"  - {issueRecords.Count} issues");
        Console.WriteLine($"  - {customFieldRecords.Count} custom fields");
        Console.WriteLine($"  - {commentRecords.Count} comments");

        return issueRecords.Count;
    }

    /// <summary>
    /// Updates the issueRecords table by migrating specific custom field data from the custom_fields table.
    /// This matches the TypeScript implementation's updateIssuesFromCustomFields function.
    /// </summary>
    /// <param name="connection">The database connection</param>
    private async Task UpdateIssuesFromCustomFieldsAsync(SqliteConnection connection)
    {
        Console.WriteLine("\nMigrating select custom fields to main issues table...");
        
        int totalUpdated = 0;
        DateTime startTime = DateTime.UtcNow;

        const string basicSelect = nameof(CustomFieldRecord.FieldValue);
        const string coalesceSelect = $"COALESCE(trim(REPLACE(REPLACE(REPLACE({nameof(CustomFieldRecord.FieldValue)}, CHAR(10), ''), CHAR(13), ''), '&amp;', '&')), '')";

        // Iterate through each custom field mapping
        foreach (JiraCustomField.CustomFieldMappingInfo fieldDef in JiraCustomField.CustomFieldMappings)
        {
            try
            {
                Console.WriteLine($"Updating column '{fieldDef.DbColumn}' from custom field '{fieldDef.FieldName}' ({fieldDef.FieldId})...");

                // Determine which select expression to use based on the field
                string selectExpression = fieldDef.UseCoalesce
                    ? coalesceSelect
                    : basicSelect;

                // Prepare the UPDATE statement to transfer data from custom fields to issues
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"""
                    UPDATE {IssueRecord.DefaultTableName} 
                    SET {fieldDef.DbColumn} = (
                        SELECT {selectExpression}
                        FROM {CustomFieldRecord.DefaultTableName}
                        WHERE {CustomFieldRecord.DefaultTableName}.{nameof(CustomFieldRecord.IssueId)} = {IssueRecord.DefaultTableName}.{nameof(IssueRecord.Id)}
                        AND {CustomFieldRecord.DefaultTableName}.{nameof(CustomFieldRecord.FieldId)} = $field_id
                    )
                    WHERE EXISTS (
                        SELECT 1 FROM {CustomFieldRecord.DefaultTableName} 
                        WHERE {CustomFieldRecord.DefaultTableName}.{nameof(CustomFieldRecord.IssueId)} = {IssueRecord.DefaultTableName}.{nameof(IssueRecord.Id)}
                        AND {CustomFieldRecord.DefaultTableName}.{nameof(CustomFieldRecord.FieldId)} = $field_id
                    )
                    """;
                
                command.Parameters.AddWithValue("$field_id", fieldDef.FieldId);
                int changes = await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"✓ Updated {changes} records for '{fieldDef.DbColumn}'");
                totalUpdated += changes;
            }
            catch (Exception error)
            {
                Console.WriteLine($"✗ Failed to update column '{fieldDef.DbColumn}': {error.Message}");
                // Continue with other fields even if one fails
            }
        }

        // remove the custom fields we have processed
        try
        {
            Console.WriteLine("Cleaning up migrated custom fields from custom_fields table...");
            using SqliteCommand deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = $"""
                DELETE FROM {CustomFieldRecord.DefaultTableName}
                WHERE {nameof(CustomFieldRecord.FieldId)} IN ({string.Join(", ", JiraCustomField.CustomFieldMappings.Select((f, i) => $"$field_id_{i}"))})
                """;
            for (int i = 0; i < JiraCustomField.CustomFieldMappings.Count; i++)
            {
                deleteCmd.Parameters.AddWithValue($"$field_id_{i}", JiraCustomField.CustomFieldMappings[i].FieldId);
            }
            int deleted = await deleteCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"✓ Deleted {deleted} migrated custom field records.");
        }
        catch (Exception error)
        {
            Console.WriteLine($"✗ Failed to clean up custom fields: {error.Message}");
        }

        // Log summary of migration
        TimeSpan elapsedTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"Migration completed: {totalUpdated} total records updated in {elapsedTime.TotalMilliseconds}ms");
    }
}
