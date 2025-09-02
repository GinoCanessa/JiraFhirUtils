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
                key TEXT,
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
                issue_key TEXT,
                field_id TEXT,
                field_key TEXT,
                field_name TEXT,
                field_value TEXT,
                FOREIGN KEY (issue_key) REFERENCES issues(key)
            );";
        command.ExecuteNonQuery();

        // Table for comments (one-to-many relationship with issues)
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS comments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                comment_id TEXT UNIQUE,
                issue_key TEXT,
                author TEXT,
                created_at TEXT,
                body TEXT,
                FOREIGN KEY (issue_key) REFERENCES issues(key)
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
            JiraRss jiraData = JiraXmlHelper.DeserializeFromFile(filePath);
            List<JiraItem> items = jiraData.Channel.Items;
            
            if (items.Count == 0)
            {
                Console.WriteLine($"No items found in {filePath}.");
                return;
            }

            // Use a transaction for bulk inserts from a single file
            using SqliteTransaction transaction = connection.BeginTransaction();

            // Prepare insert statements for performance
            SqliteCommand insertIssueCommand = connection.CreateCommand();
            insertIssueCommand.Transaction = transaction;
            insertIssueCommand.CommandText = @"
                INSERT OR IGNORE INTO issues (
                    key, id, title, issue_url, 
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
                    $key, $id, $title, $issue_url, 
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
            AddIssueInsertParameters(insertIssueCommand);

            SqliteCommand insertCustomFieldCommand = connection.CreateCommand();
            insertCustomFieldCommand.Transaction = transaction;
            insertCustomFieldCommand.CommandText = @"
                INSERT INTO custom_fields (issue_key, field_id, field_key, field_name, field_value)
                VALUES ($issue_key, $field_id, $field_key, $field_name, $field_value)";
            insertCustomFieldCommand.Parameters.AddWithValue("$issue_key", "");
            insertCustomFieldCommand.Parameters.AddWithValue("$field_id", "");
            insertCustomFieldCommand.Parameters.AddWithValue("$field_key", "");
            insertCustomFieldCommand.Parameters.AddWithValue("$field_name", "");
            insertCustomFieldCommand.Parameters.AddWithValue("$field_value", "");

            SqliteCommand insertCommentCommand = connection.CreateCommand();
            insertCommentCommand.Transaction = transaction;
            insertCommentCommand.CommandText = @"
                INSERT OR IGNORE INTO comments (comment_id, issue_key, author, created_at, body)
                VALUES ($comment_id, $issue_key, $author, $created_at, $body)";
            insertCommentCommand.Parameters.AddWithValue("$comment_id", "");
            insertCommentCommand.Parameters.AddWithValue("$issue_key", "");
            insertCommentCommand.Parameters.AddWithValue("$author", "");
            insertCommentCommand.Parameters.AddWithValue("$created_at", "");
            insertCommentCommand.Parameters.AddWithValue("$body", "");

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
                ProcessIssue(item, issueKey, insertIssueCommand);
                await insertIssueCommand.ExecuteNonQueryAsync();

                // Process custom fields
                await ProcessCustomFields(item, issueKey, insertCustomFieldCommand);

                // Process comments
                await ProcessComments(item, issueKey, insertCommentCommand);
                
                processedCount++;
            }

            await transaction.CommitAsync();
            Console.WriteLine($"Successfully inserted or updated {processedCount} issues from {filePath}.");

        }
        catch (Exception error)
        {
            Console.WriteLine($"Failed to process file {filePath}: {error.Message}");
        }
    }

    /// <summary>
    /// Adds all required parameters to the insert issue command.
    /// </summary>
    /// <param name="command">The command to add parameters to</param>
    private void AddIssueInsertParameters(SqliteCommand command)
    {
        command.Parameters.AddWithValue("$key", "");
        command.Parameters.AddWithValue("$id", "");
        command.Parameters.AddWithValue("$title", "");
        command.Parameters.AddWithValue("$issue_url", "");
        command.Parameters.AddWithValue("$project_id", "");
        command.Parameters.AddWithValue("$project_key", "");
        command.Parameters.AddWithValue("$description", "");
        command.Parameters.AddWithValue("$summary", "");
        command.Parameters.AddWithValue("$type", "");
        command.Parameters.AddWithValue("$type_id", "");
        command.Parameters.AddWithValue("$priority", "");
        command.Parameters.AddWithValue("$priority_id", "");
        command.Parameters.AddWithValue("$status", "");
        command.Parameters.AddWithValue("$status_id", "");
        command.Parameters.AddWithValue("$status_category_id", "");
        command.Parameters.AddWithValue("$status_category_key", "");
        command.Parameters.AddWithValue("$status_category_color", "");
        command.Parameters.AddWithValue("$resolution", "");
        command.Parameters.AddWithValue("$resolution_id", "");
        command.Parameters.AddWithValue("$assignee", "");
        command.Parameters.AddWithValue("$reporter", "");
        command.Parameters.AddWithValue("$created_at", "");
        command.Parameters.AddWithValue("$updated_at", "");
        command.Parameters.AddWithValue("$resolved_at", "");
        command.Parameters.AddWithValue("$watches", "");
        command.Parameters.AddWithValue("$specification", "");
        command.Parameters.AddWithValue("$appliedForVersion", "");
        command.Parameters.AddWithValue("$changeCategory", "");
        command.Parameters.AddWithValue("$changeImpact", "");
        command.Parameters.AddWithValue("$duplicateIssue", "");
        command.Parameters.AddWithValue("$grouping", "");
        command.Parameters.AddWithValue("$raisedInVersion", "");
        command.Parameters.AddWithValue("$relatedIssues", "");
        command.Parameters.AddWithValue("$relatedArtifacts", "");
        command.Parameters.AddWithValue("$relatedPages", "");
        command.Parameters.AddWithValue("$relatedSections", "");
        command.Parameters.AddWithValue("$relatedURL", "");
        command.Parameters.AddWithValue("$resolutionDescription", "");
        command.Parameters.AddWithValue("$voteDate", "");
        command.Parameters.AddWithValue("$vote", "");
        command.Parameters.AddWithValue("$workGroup", "");
    }

    /// <summary>
    /// Processes an issue element and populates the insert command parameters.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="command">The insert command to populate</param>
    private void ProcessIssue(JiraItem item, string issueKey, SqliteCommand command)
    {
        // Set parameter values
        command.Parameters["$key"].Value = issueKey;
        command.Parameters["$id"].Value = item.Key.Id != 0 ? item.Key.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$title"].Value = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : (object)DBNull.Value;
        command.Parameters["$issue_url"].Value = !string.IsNullOrWhiteSpace(item.Link) ? item.Link : (object)DBNull.Value;
        command.Parameters["$project_id"].Value = item.Project.Id != 0 ? item.Project.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$project_key"].Value = !string.IsNullOrWhiteSpace(item.Project.Key) ? item.Project.Key : (object)DBNull.Value;
        command.Parameters["$description"].Value = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : (object)DBNull.Value;
        command.Parameters["$summary"].Value = !string.IsNullOrWhiteSpace(item.Summary) ? item.Summary : (object)DBNull.Value;
        command.Parameters["$type"].Value = !string.IsNullOrWhiteSpace(item.Type.Name) ? item.Type.Name : (object)DBNull.Value;
        command.Parameters["$type_id"].Value = item.Type.Id != 0 ? item.Type.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$priority"].Value = !string.IsNullOrWhiteSpace(item.Priority.Name) ? item.Priority.Name : (object)DBNull.Value;
        command.Parameters["$priority_id"].Value = item.Priority.Id != 0 ? item.Priority.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$status"].Value = !string.IsNullOrWhiteSpace(item.Status.Name) ? item.Status.Name : (object)DBNull.Value;
        command.Parameters["$status_id"].Value = item.Status.Id != 0 ? item.Status.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$status_category_id"].Value = item.Status.StatusCategory?.Id != 0 ? item.Status.StatusCategory?.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$status_category_key"].Value = !string.IsNullOrWhiteSpace(item.Status.StatusCategory?.Key) ? item.Status.StatusCategory.Key : (object)DBNull.Value;
        command.Parameters["$status_category_color"].Value = !string.IsNullOrWhiteSpace(item.Status.StatusCategory?.ColorName) ? item.Status.StatusCategory.ColorName : (object)DBNull.Value;
        command.Parameters["$resolution"].Value = !string.IsNullOrWhiteSpace(item.Resolution?.Name) ? item.Resolution.Name : (object)DBNull.Value;
        command.Parameters["$resolution_id"].Value = item.Resolution?.Id != 0 ? item.Resolution?.Id.ToString() : (object)DBNull.Value;
        command.Parameters["$assignee"].Value = !string.IsNullOrWhiteSpace(item.Assignee?.Username) ? item.Assignee.Username : (object)DBNull.Value;
        command.Parameters["$reporter"].Value = !string.IsNullOrWhiteSpace(item.Reporter?.Username) ? item.Reporter.Username : (object)DBNull.Value;
        command.Parameters["$created_at"].Value = ToIsoString(item.Created) ?? (object)DBNull.Value;
        command.Parameters["$updated_at"].Value = ToIsoString(item.Updated) ?? (object)DBNull.Value;
        command.Parameters["$resolved_at"].Value = ToIsoString(item.Resolved) ?? (object)DBNull.Value;

        command.Parameters["$watches"].Value = item.Watches != 0 ? item.Watches : (object)DBNull.Value;

        // Initialize custom fields to null - they will be populated later
        command.Parameters["$specification"].Value = DBNull.Value;
        command.Parameters["$appliedForVersion"].Value = DBNull.Value;
        command.Parameters["$changeCategory"].Value = DBNull.Value;
        command.Parameters["$changeImpact"].Value = DBNull.Value;
        command.Parameters["$duplicateIssue"].Value = DBNull.Value;
        command.Parameters["$grouping"].Value = DBNull.Value;
        command.Parameters["$raisedInVersion"].Value = DBNull.Value;
        command.Parameters["$relatedIssues"].Value = DBNull.Value;
        command.Parameters["$relatedArtifacts"].Value = DBNull.Value;
        command.Parameters["$relatedPages"].Value = DBNull.Value;
        command.Parameters["$relatedSections"].Value = DBNull.Value;
        command.Parameters["$relatedURL"].Value = DBNull.Value;
        command.Parameters["$resolutionDescription"].Value = DBNull.Value;
        command.Parameters["$voteDate"].Value = DBNull.Value;
        command.Parameters["$vote"].Value = DBNull.Value;
        command.Parameters["$workGroup"].Value = DBNull.Value;
    }

    /// <summary>
    /// Processes custom fields for an issue.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="command">The insert custom field command</param>
    private async Task ProcessCustomFields(JiraItem item, string issueKey, SqliteCommand command)
    {
        List<JiraXmlCustomField> customFields = item.CustomFields?.CustomFieldList ?? new List<JiraXmlCustomField>();
        
        foreach (JiraXmlCustomField field in customFields)
        {
            string? fieldId = !string.IsNullOrWhiteSpace(field.Id) ? field.Id : null;
            string? fieldKey = !string.IsNullOrWhiteSpace(field.Key) ? field.Key : null;
            string? fieldName = !string.IsNullOrWhiteSpace(field.CustomFieldName) ? field.CustomFieldName : null;

            // Process custom field values
            List<JiraCustomFieldValue> customFieldValues = field.CustomFieldValues?.Values ?? new List<JiraCustomFieldValue>();
            string? value = null;
            
            if (customFieldValues.Count > 1)
            {
                // Handle array of values
                value = string.Join(", ", customFieldValues.Select(v => v.Value));
            }
            else if (customFieldValues.Count == 1)
            {
                value = customFieldValues.First().Value;
            }

            // Insert the custom field record
            command.Parameters["$issue_key"].Value = issueKey;
            command.Parameters["$field_id"].Value = fieldId ?? (object)DBNull.Value;
            command.Parameters["$field_key"].Value = fieldKey ?? (object)DBNull.Value;
            command.Parameters["$field_name"].Value = fieldName ?? (object)DBNull.Value;
            command.Parameters["$field_value"].Value = value ?? (object)DBNull.Value;
            
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Processes comments for an issue.
    /// </summary>
    /// <param name="item">The JIRA item object</param>
    /// <param name="issueKey">The issue key</param>
    /// <param name="command">The insert comment command</param>
    private async Task ProcessComments(JiraItem item, string issueKey, SqliteCommand command)
    {
        List<JiraComment> comments = item.Comments?.CommentList ?? new List<JiraComment>();
        
        foreach (JiraComment comment in comments)
        {
            string? commentId = comment.Id != 0 ? comment.Id.ToString() : null;
            string? author = !string.IsNullOrWhiteSpace(comment.Author) ? comment.Author : null;
            string? created = !string.IsNullOrWhiteSpace(comment.Created) ? comment.Created : null;
            string? body = !string.IsNullOrWhiteSpace(comment.Body) ? comment.Body : null;

            // Insert the comment record
            command.Parameters["$comment_id"].Value = commentId ?? (object)DBNull.Value;
            command.Parameters["$issue_key"].Value = issueKey;
            command.Parameters["$author"].Value = author ?? (object)DBNull.Value;
            command.Parameters["$created_at"].Value = ToIsoString(created) ?? (object)DBNull.Value;
            command.Parameters["$body"].Value = body ?? (object)DBNull.Value;
            
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
        foreach ((string dbColumn, JiraCustomField.CustomFieldInfo fieldDef) in JiraCustomField.DbFieldToCustomFieldName)
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
}
