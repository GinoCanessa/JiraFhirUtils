using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using JiraFhirUtils.Common;
using System.Globalization;
using System.Xml.Serialization;

namespace jf_loader.Load;

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
        
        foreach (string filePath in xmlFiles)
        {
            await ProcessXmlFileAsync(filePath, connection);
        }
        
        // Migrate custom field data from custom_fields table to issues table
        await UpdateIssuesFromCustomFieldsAsync(connection);
        
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

        // Main table for JIRA issues
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS issues (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                key TEXT UNIQUE,
                title TEXT,
                issue_url TEXT,
                project_id INTEGER,
                project_key TEXT,
                description TEXT,
                summary TEXT,
                type TEXT,
                type_id INTEGER,
                priority TEXT,
                priority_id INTEGER,
                status TEXT,
                status_id INTEGER,
                status_category_id INTEGER,
                status_category_key TEXT,
                status_category_color TEXT,
                resolution TEXT,
                resolution_id INTEGER,
                assignee TEXT,
                reporter TEXT,
                created_at TEXT,
                updated_at TEXT,
                resolved_at TEXT,
                watches INTEGER,
                specification TEXT,
                appliedForVersion TEXT,
                changeCategory TEXT,
                changeImpact TEXT,
                duplicateIssue TEXT,
                grouping TEXT,
                raisedInVersion TEXT,
                relatedIssues TEXT,
                relatedArtifacts TEXT,
                relatedPages TEXT,
                relatedSections TEXT,
                relatedURL TEXT,
                resolutionDescription TEXT,
                voteDate TEXT,
                vote TEXT,
                workGroup TEXT
            );";
        command.ExecuteNonQuery();

        // Table for custom fields (one-to-many relationship with issues)
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS custom_fields (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                issue_id INTEGER,
                issue_key TEXT,
                field_id TEXT,
                field_key TEXT,
                field_name TEXT,
                field_value TEXT,
                FOREIGN KEY (issue_id) REFERENCES issues(id)
            );";
        command.ExecuteNonQuery();

        // Table for comments (one-to-many relationship with issues)
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS comments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                comment_id TEXT UNIQUE,
                issue_id INTEGER,
                issue_key TEXT,
                author TEXT,
                created_at TEXT,
                body TEXT,
                FOREIGN KEY (issue_id) REFERENCES issues(id)
            );";
        command.ExecuteNonQuery();

        CreateCommentsIndexes(connection);
        
        Console.WriteLine("Database schema is ready.");
    }

    /// <summary>
    /// Creates indexes for the comments table to optimize query performance.
    /// </summary>
    /// <param name="connection">The database connection</param>
    private void CreateCommentsIndexes(SqliteConnection connection)
    {
        Console.WriteLine("Creating comments table indexes...");
        
        using SqliteCommand command = connection.CreateCommand();
        
        // Critical index for JOIN performance in loadIssues query
        command.CommandText = "CREATE INDEX IF NOT EXISTS idx_comments_issue_key ON comments(issue_key)";
        command.ExecuteNonQuery();
        Console.WriteLine("✓ Created index: idx_comments_issue_key");
        
        // Covering index for GROUP_CONCAT optimization
        command.CommandText = "CREATE INDEX IF NOT EXISTS idx_comments_issue_key_body ON comments(issue_key, body)";
        command.ExecuteNonQuery();
        Console.WriteLine("✓ Created index: idx_comments_issue_key_body");
        
        Console.WriteLine("Comments indexes created successfully.");
    }

    /// <summary>
    /// Processes a single XML file and loads its data into the database.
    /// </summary>
    /// <param name="filePath">The path to the XML file</param>
    /// <param name="connection">The database connection</param>
    private async Task ProcessXmlFileAsync(string filePath, SqliteConnection connection)
    {
        Console.WriteLine($"\nProcessing file: {filePath}");

        try
        {
            // Load and validate JIRA data from the XML file
            List<JiraItem> items;
            try
            {
                items = LoadJiraDataFromFile(filePath);
            }
            catch (InvalidOperationException)
            {
                // Handle empty file case - already logged in LoadJiraDataFromFile
                Console.WriteLine($"No items found in {filePath}.");
                return;
            }

            // Prepare SQL commands for bulk operations  
            (SqliteCommand issueCommand, SqliteCommand customFieldCommand, SqliteCommand commentCommand) commands = PrepareInsertCommands(connection);

            // Process all items within the transaction
            await ProcessBatchWithTransaction(items, connection, commands, filePath);
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
    private List<JiraItem> LoadJiraDataFromFile(string filePath)
    {
        JiraRss jiraData = JiraXmlHelper.DeserializeFromFile(filePath);
        List<JiraItem> items = jiraData.Channel.Items;
        
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"No items found in {filePath}.");
        }

        return items;
    }

    /// <summary>
    /// Prepares the SQL insert commands for bulk operations.
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <returns>Tuple containing issue, custom field, and comment insert commands</returns>
    private (SqliteCommand issueCommand, SqliteCommand customFieldCommand, SqliteCommand commentCommand) 
        PrepareInsertCommands(SqliteConnection connection)
    {
        // Prepare insert statement for issues
        SqliteCommand insertIssueCommand = connection.CreateCommand();
        insertIssueCommand.CommandText = @"
            INSERT OR IGNORE INTO issues (
                key, title, issue_url, 
                project_id, project_key, description, summary, 
                type, type_id, priority, priority_id, 
                status, status_id, status_category_id, status_category_key, 
                status_category_color, resolution, resolution_id, assignee, 
                reporter, created_at, updated_at, resolved_at, 
                watches, specification, appliedForVersion, changeCategory, 
                changeImpact, duplicateIssue, grouping, raisedInVersion, 
                relatedIssues, relatedArtifacts, relatedPages, relatedSections, 
                relatedURL, resolutionDescription, voteDate, vote, 
                workGroup
            ) VALUES (
                $key, $title, $issue_url, 
                $project_id, $project_key, $description, $summary, 
                $type, $type_id, $priority, $priority_id, 
                $status, $status_id, $status_category_id, $status_category_key, 
                $status_category_color, $resolution, $resolution_id, $assignee, 
                $reporter, $created_at, $updated_at, $resolved_at, 
                $watches, $specification, $appliedForVersion, $changeCategory,
                $changeImpact, $duplicateIssue, $grouping, $raisedInVersion,
                $relatedIssues, $relatedArtifacts, $relatedPages, $relatedSections,
                $relatedURL, $resolutionDescription, $voteDate, $vote,
                $workGroup
            )";

        // Prepare insert statement for custom fields
        SqliteCommand insertCustomFieldCommand = connection.CreateCommand();
        insertCustomFieldCommand.CommandText = @"
            INSERT INTO custom_fields (issue_id, issue_key, field_id, field_key, field_name, field_value)
            VALUES ($issue_id, $issue_key, $field_id, $field_key, $field_name, $field_value)";
        insertCustomFieldCommand.Parameters.AddWithValue("$issue_id", "");
        insertCustomFieldCommand.Parameters.AddWithValue("$issue_key", "");
        insertCustomFieldCommand.Parameters.AddWithValue("$field_id", "");
        insertCustomFieldCommand.Parameters.AddWithValue("$field_key", "");
        insertCustomFieldCommand.Parameters.AddWithValue("$field_name", "");
        insertCustomFieldCommand.Parameters.AddWithValue("$field_value", "");

        // Prepare insert statement for comments
        SqliteCommand insertCommentCommand = connection.CreateCommand();
        insertCommentCommand.CommandText = @"
            INSERT OR IGNORE INTO comments (comment_id, issue_id, issue_key, author, created_at, body)
            VALUES ($comment_id, $issue_id, $issue_key, $author, $created_at, $body)";
        insertCommentCommand.Parameters.AddWithValue("$comment_id", "");
        insertCommentCommand.Parameters.AddWithValue("$issue_id", "");
        insertCommentCommand.Parameters.AddWithValue("$issue_key", "");
        insertCommentCommand.Parameters.AddWithValue("$author", "");
        insertCommentCommand.Parameters.AddWithValue("$created_at", "");
        insertCommentCommand.Parameters.AddWithValue("$body", "");

        return (insertIssueCommand, insertCustomFieldCommand, insertCommentCommand);
    }

    /// <summary>
    /// Processes a batch of JIRA items within a database transaction.
    /// </summary>
    /// <param name="items">List of JIRA items to process</param>
    /// <param name="connection">The database connection</param>
    /// <param name="commands">Tuple containing the prepared SQL commands</param>
    /// <param name="filePath">The file path for logging purposes</param>
    /// <returns>Number of items processed</returns>
    private async Task<int> ProcessBatchWithTransaction(
        List<JiraItem> items,
        SqliteConnection connection,
        (SqliteCommand issueCommand, SqliteCommand customFieldCommand, SqliteCommand commentCommand) commands,
        string filePath)
    {
        try
        {
            // Use a transaction for bulk inserts from a single file
            using SqliteTransaction transaction = connection.BeginTransaction();

            // Set transaction for all commands
            commands.issueCommand.Transaction = transaction;
            commands.customFieldCommand.Transaction = transaction;
            commands.commentCommand.Transaction = transaction;

            int processedCount = 0;

            foreach (JiraItem item in items)
            {
                string? issueKey = item.Key.Value;
                if (string.IsNullOrWhiteSpace(issueKey))
                {
                    Console.WriteLine($"Skipping item with missing key in {filePath}: {item.Title ?? "Unknown Title"}");
                    continue;
                }

                // Process the issue
                try
                {
                    ProcessIssue(item, issueKey, commands.issueCommand);
                    await commands.issueCommand.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing issue '{issueKey}' in {filePath}: {ex.Message}");
                    continue; // Skip this item and continue with the next
                }

                // Process custom fields
                try
                {
                    await ProcessCustomFields(item, issueKey, connection, commands.customFieldCommand);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing custom fields for issue '{issueKey}' in {filePath}: {ex.Message}");
                    continue; // Skip this item and continue with the next
                }

                // Process comments
                try
                {
                    await ProcessComments(item, issueKey, connection, commands.commentCommand);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing comments for issue '{issueKey}' in {filePath}: {ex.Message}");
                    continue; // Skip this item and continue with the next
                }

                processedCount++;
            }

            await transaction.CommitAsync();
            Console.WriteLine($"Successfully inserted or updated {processedCount} issues from {filePath}.");

            return processedCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing batch from {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Processes an issue element and populates the insert command parameters.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="command">The insert command to populate</param>
    private void ProcessIssue(JiraItem item, string issueKey, SqliteCommand command)
    {
        // Map JIRA item to database record using extension method and bind parameters
        IssueRecord issueRecord = item.ToIssueRecord(issueKey);
        BindIssueRecordParameters(command, issueRecord);
    }

    /// <summary>
    /// Processes custom fields for an issue.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="connection">The database connection</param>
    /// <param name="command">The insert custom field command</param>
    private async Task ProcessCustomFields(JiraItem item, string issueKey, SqliteConnection connection, SqliteCommand command)
    {
        List<JiraXmlCustomField> customFields = item.CustomFields?.CustomFieldList ?? new List<JiraXmlCustomField>();
        
        // Resolve issue key to database ID
        int? issueId = await ResolveIssueKeyToIdAsync(connection, issueKey);
        if (issueId == null)
        {
            Console.WriteLine($"Warning: Could not resolve issue key '{issueKey}' to database ID. Skipping custom fields.");
            return;
        }
        
        foreach (JiraXmlCustomField field in customFields)
        {
            // Convert to database record using extension method and bind parameters
            CustomFieldRecord customFieldRecord = field.ToCustomFieldRecord(issueKey) with { IssueId = issueId.Value };
            BindCustomFieldRecordParameters(command, customFieldRecord);
            
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Processes comments for an issue.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="connection">The database connection</param>
    /// <param name="command">The insert comment command</param>
    private async Task ProcessComments(JiraItem item, string issueKey, SqliteConnection connection, SqliteCommand command)
    {
        List<JiraComment> comments = item.Comments?.CommentList ?? new List<JiraComment>();
        
        // Resolve issue key to database ID
        int? issueId = await ResolveIssueKeyToIdAsync(connection, issueKey);
        if (issueId == null)
        {
            Console.WriteLine($"Warning: Could not resolve issue key '{issueKey}' to database ID. Skipping comments.");
            return;
        }
        
        foreach (JiraComment comment in comments)
        {
            // Convert to database record using extension method and bind parameters
            CommentRecord commentRecord = comment.ToCommentRecord(issueKey) with { IssueId = issueId.Value };
            BindCommentRecordParameters(command, commentRecord);
            
            await command.ExecuteNonQueryAsync();
        }
    }


    /// <summary>
    /// Parses a date string and returns it in ISO 8601 format.
    /// Returns null if the date is invalid or not provided.
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <returns>ISO 8601 formatted date string or null</returns>
    private string? ToIsoString(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime date))
        {
            return date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
        
        return null;
    }

    /// <summary>
    /// Updates the issues table by migrating specific custom field data from the custom_fields table.
    /// This matches the TypeScript implementation's updateIssuesFromCustomFields function.
    /// </summary>
    /// <param name="connection">The database connection</param>
    private async Task UpdateIssuesFromCustomFieldsAsync(SqliteConnection connection)
    {
        Console.WriteLine("\nMigrating select custom fields to main issues table...");
        
        int totalUpdated = 0;
        DateTime startTime = DateTime.UtcNow;

        const string basicSelect = "field_value";
        const string cleanedSelect = "COALESCE(trim(REPLACE(REPLACE(REPLACE(field_value, CHAR(10), ''), CHAR(13), ''), '&amp;', '&')), '')";

        // Iterate through each custom field mapping
        foreach ((string dbColumn, JiraCustomField.CustomFieldMappingInfo fieldDef) in JiraCustomField.CustomFieldMappings)
        {
            try
            {
                Console.WriteLine($"Updating column '{dbColumn}' from custom field '{fieldDef.FieldName}' ({fieldDef.FieldId})...");

                // Determine which select expression to use based on the field
                string selectExpression = dbColumn switch
                {
                    "workGroup" or "relatedArtifacts" or "relatedPages" => cleanedSelect,
                    _ => basicSelect
                };

                // Prepare the UPDATE statement to transfer data from custom_fields to issues
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $@"
                    UPDATE issues 
                    SET {dbColumn} = (
                        SELECT {selectExpression} 
                        FROM custom_fields 
                        WHERE custom_fields.issue_key = issues.key 
                        AND custom_fields.field_id = $field_id
                    )
                    WHERE EXISTS (
                        SELECT 1 FROM custom_fields 
                        WHERE custom_fields.issue_key = issues.key 
                        AND custom_fields.field_id = $field_id
                    )";
                
                command.Parameters.AddWithValue("$field_id", fieldDef.FieldId);
                int changes = await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"✓ Updated {changes} records for '{dbColumn}'");
                totalUpdated += changes;
            }
            catch (Exception error)
            {
                Console.WriteLine($"✗ Failed to update column '{dbColumn}': {error.Message}");
                // Continue with other fields even if one fails
            }
        }

        TimeSpan elapsedTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"Migration completed: {totalUpdated} total records updated in {elapsedTime.TotalMilliseconds}ms");
    }



    /// <summary>
    /// Resolves an issue key to its database ID.
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="issueKey">The issue key to resolve</param>
    /// <returns>The database ID for the issue, or null if not found</returns>
    private static async Task<int?> ResolveIssueKeyToIdAsync(SqliteConnection connection, string issueKey)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM issues WHERE key = $key";
        command.Parameters.AddWithValue("$key", issueKey);
        
        object? result = await command.ExecuteScalarAsync();
        return result is long id ? (int)id : null;
    }

    /// <summary>
    /// Helper method to bind IssueRecord properties to SQLite command parameters
    /// </summary>
    /// <param name="command">The SQLite command to bind parameters to</param>
    /// <param name="issueRecord">The issue record with values to bind</param>
    private static void BindIssueRecordParameters(SqliteCommand command, IssueRecord issueRecord)
    {
        command.AddParameterWithValue("$key", issueRecord.Key);
        command.AddParameterWithValue("$title", issueRecord.Title);
        command.AddParameterWithValue("$issue_url", issueRecord.IssueUrl);
        command.AddParameterWithValue("$project_id", issueRecord.ProjectId);
        command.AddParameterWithValue("$project_key", issueRecord.ProjectKey);
        command.AddParameterWithValue("$description", issueRecord.Description);
        command.AddParameterWithValue("$summary", issueRecord.Summary);
        command.AddParameterWithValue("$type", issueRecord.Type);
        command.AddParameterWithValue("$type_id", issueRecord.TypeId);
        command.AddParameterWithValue("$priority", issueRecord.Priority);
        command.AddParameterWithValue("$priority_id", issueRecord.PriorityId);
        command.AddParameterWithValue("$status", issueRecord.Status);
        command.AddParameterWithValue("$status_id", issueRecord.StatusId);
        command.AddParameterWithValue("$status_category_id", issueRecord.StatusCategoryId);
        command.AddParameterWithValue("$status_category_key", issueRecord.StatusCategoryKey);
        command.AddParameterWithValue("$status_category_color", issueRecord.StatusCategoryColor);
        command.AddParameterWithValue("$resolution", issueRecord.Resolution);
        command.AddParameterWithValue("$resolution_id", issueRecord.ResolutionId);
        command.AddParameterWithValue("$assignee", issueRecord.Assignee);
        command.AddParameterWithValue("$reporter", issueRecord.Reporter);
        command.Parameters.AddWithValue("$created_at", SqliteParameterExtensions.FormatDateTimeForDatabase(issueRecord.CreatedAt));
        command.Parameters.AddWithValue("$updated_at", SqliteParameterExtensions.FormatDateTimeForDatabase(issueRecord.UpdatedAt));
        command.Parameters.AddWithValue("$resolved_at", SqliteParameterExtensions.FormatDateTimeForDatabase(issueRecord.ResolvedAt));
        command.AddParameterWithValue("$watches", issueRecord.Watches);
        command.AddParameterWithValue("$specification", issueRecord.Specification);
        command.AddParameterWithValue("$appliedForVersion", issueRecord.AppliedForVersion);
        command.AddParameterWithValue("$changeCategory", issueRecord.ChangeCategory);
        command.AddParameterWithValue("$changeImpact", issueRecord.ChangeImpact);
        command.AddParameterWithValue("$duplicateIssue", issueRecord.DuplicateIssue);
        command.AddParameterWithValue("$grouping", issueRecord.Grouping);
        command.AddParameterWithValue("$raisedInVersion", issueRecord.RaisedInVersion);
        command.AddParameterWithValue("$relatedIssues", issueRecord.RelatedIssues);
        command.AddParameterWithValue("$relatedArtifacts", issueRecord.RelatedArtifacts);
        command.AddParameterWithValue("$relatedPages", issueRecord.RelatedPages);
        command.AddParameterWithValue("$relatedSections", issueRecord.RelatedSections);
        command.AddParameterWithValue("$relatedURL", issueRecord.RelatedURL);
        command.AddParameterWithValue("$resolutionDescription", issueRecord.ResolutionDescription);
        command.Parameters.AddWithValue("$voteDate", SqliteParameterExtensions.FormatDateTimeForDatabase(issueRecord.VoteDate));
        command.AddParameterWithValue("$vote", issueRecord.Vote);
        command.AddParameterWithValue("$workGroup", issueRecord.WorkGroup);
    }

    /// <summary>
    /// Helper method to bind CustomFieldRecord properties to SQLite command parameters
    /// </summary>
    /// <param name="command">The SQLite command to bind parameters to</param>
    /// <param name="customFieldRecord">The custom field record with values to bind</param>
    private static void BindCustomFieldRecordParameters(SqliteCommand command, CustomFieldRecord customFieldRecord)
    {
        command.SetParameterValue("$issue_id", customFieldRecord.IssueId);
        command.SetParameterValue("$issue_key", customFieldRecord.IssueKey);
        command.SetParameterValue("$field_id", customFieldRecord.FieldId);
        command.SetParameterValue("$field_key", customFieldRecord.FieldKey);
        command.SetParameterValue("$field_name", customFieldRecord.FieldName);
        command.SetParameterValue("$field_value", customFieldRecord.FieldValue);
    }

    /// <summary>
    /// Helper method to bind CommentRecord properties to SQLite command parameters
    /// </summary>
    /// <param name="command">The SQLite command to bind parameters to</param>
    /// <param name="commentRecord">The comment record with values to bind</param>
    private static void BindCommentRecordParameters(SqliteCommand command, CommentRecord commentRecord)
    {
        command.SetParameterValue("$comment_id", commentRecord.JiraCommentId != 0 ? commentRecord.JiraCommentId.ToString() : null);
        command.SetParameterValue("$issue_id", commentRecord.IssueId);
        command.SetParameterValue("$issue_key", commentRecord.IssueKey);
        command.SetParameterValue("$author", commentRecord.Author);
        command.Parameters["$created_at"].Value = SqliteParameterExtensions.FormatDateTimeForDatabase(commentRecord.CreatedAt);
        command.SetParameterValue("$body", commentRecord.Body);
    }
}
