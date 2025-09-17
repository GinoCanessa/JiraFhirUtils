using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for finding and returning full issue records related to a specific JIRA issue through explicit links and keyword similarity
/// </summary>
public class FindRelatedIssuesTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "find_related_issues";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "Find and return full issue records related to a specific JIRA issue through explicit links and keyword similarity. Returns complete issue objects for both explicitly linked issues and keyword-related issues.";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [
        new ToolArgumentRec("issue_key", "string", "The issue key to find related issues for"),
        new ToolArgumentRec("limit", "number", "Maximum number of related issues to return (default: 10)")
    ];

    /// <summary>
    /// Required argument names for validation
    /// </summary>
    protected override string[] RequiredArguments => ["issue_key"];

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public FindRelatedIssuesTool() : base()
    {
    }

    /// <summary>
    /// Execute the find related issues tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with related issues list or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        string issueKey = GetArgumentValue<string>(arguments, "issue_key")!;
        int limit = GetArgumentValue<int?>(arguments, "limit") ?? 10;

        // Validate limit
        if (limit <= 0)
        {
            return CreateErrorResponse("Limit must be greater than 0");
        }

        if (limit > 100)
        {
            return CreateErrorResponse("Limit cannot exceed 100");
        }

        try
        {
            // Step 1: Retrieve the source issue
            IssueRecord? sourceIssue = IssueRecord.SelectSingle(DatabaseService.Instance.Db, Key: issueKey);
            if (sourceIssue == null)
            {
                return CreateErrorResponse($"Source issue not found: {issueKey}");
            }

            // Step 2: Get explicit linked issues as full records
            List<IssueRecord> linkedIssues = [];
            if (!string.IsNullOrWhiteSpace(sourceIssue.RelatedIssues))
            {
                List<string> linkedIssueKeys = sourceIssue.RelatedIssues
                    .Split(',')
                    .Select(issue => issue.Trim())
                    .Where(issue => !string.IsNullOrWhiteSpace(issue))
                    .ToList();

                foreach (string linkedKey in linkedIssueKeys)
                {
                    IssueRecord? linkedIssue = IssueRecord.SelectSingle(DatabaseService.Instance.Db, Key: linkedKey);
                    if (linkedIssue != null)
                    {
                        linkedIssues.Add(linkedIssue);
                    }
                }
            }

            // Step 3: Get top 3 keywords for the issue
            List<DbIssueKeywordRecord> keywordRecords = DbIssueKeywordRecord.SelectList(
                DatabaseService.Instance.Db,
                IssueId: sourceIssue.Id,
                resultLimit: 3,
                orderByProperties: [nameof(DbIssueKeywordRecord.Bm25Score)],
                orderByDirection: "DESC"
            );

            string keywords = string.Join(" OR ", keywordRecords.Select(k => k.Keyword));

            // Step 4: Query related issues using field matching
            List<IssueRecord> fieldRelatedIssues = findFieldRelatedIssues(sourceIssue, issueKey, limit);

            // Step 5: Query related issues using keyword matching (FTS)
            List<IssueRecord> keywordRelatedIssues = [];
            if (!string.IsNullOrWhiteSpace(keywords))
            {
                keywordRelatedIssues = FindKeywordRelatedIssues(keywords, issueKey, limit);
            }

            // Step 6: Combine and deduplicate results
            Dictionary<string, IssueRecord> allMatches = new Dictionary<string, IssueRecord>();

            // Add field matches
            foreach (IssueRecord issue in fieldRelatedIssues)
            {
                if (issue.Key != issueKey)
                {
                    allMatches[issue.Key] = issue;
                }
            }

            // Add keyword matches
            foreach (IssueRecord issue in keywordRelatedIssues)
            {
                if (issue.Key != issueKey)
                {
                    allMatches[issue.Key] = issue;
                }
            }

            // Convert to list and limit results
            List<IssueRecord> finalMatches = allMatches.Values.Take(limit).ToList();

            // Create response object
            var response = new
            {
                issue_key = issueKey,
                total_linked = linkedIssues.Count,
                total_keyword_related = finalMatches.Count,
                keywords = keywords,
                issues_linked = linkedIssues,
                issues_keyword_related = finalMatches
            };

            return CreateSuccessResponse(response);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error finding related issues: {ex.Message}");
        }
    }

    /// <summary>
    /// Find issues related through field matching (relatedUrl, relatedArtifacts, relatedPages)
    /// Returns full IssueRecord objects
    /// </summary>
    private List<IssueRecord> findFieldRelatedIssues(IssueRecord sourceIssue, string excludeKey, int limit)
    {
        List<string> conditions = [];
        List<SqliteParameter> parameters = [];

        // Match project_key and work_group
        conditions.Add($"{nameof(IssueRecord.ProjectKey)} = @project_key");
        conditions.Add($"{nameof(IssueRecord.WorkGroup)} = @work_group");
        conditions.Add($"{nameof(IssueRecord.Key)} != @exclude_key");

        parameters.Add(new SqliteParameter("@project_key", sourceIssue.ProjectKey));
        parameters.Add(new SqliteParameter("@work_group", sourceIssue.WorkGroup));
        parameters.Add(new SqliteParameter("@exclude_key", excludeKey));

        // Add relatedUrl, relatedArtifacts, relatedPages if present
        string[] fieldNames =[ nameof(IssueRecord.RelatedUrl), nameof(IssueRecord.RelatedArtifacts), nameof(IssueRecord.RelatedIssues) ];
        string?[] fieldValues = [ sourceIssue.RelatedUrl, sourceIssue.RelatedArtifacts, sourceIssue.RelatedPages ];

        for (int i = 0; i < fieldNames.Length; i++)
        {
            string fieldName = fieldNames[i];
            string? fieldValue = fieldValues[i];

            if (string.IsNullOrWhiteSpace(fieldValue))
                continue;

            if (fieldValue.Contains(','))
            {
                // Multiple terms separated by commas
                IEnumerable<string> terms = fieldValue
                    .Split(',')
                    .Select(term => term.Trim())
                    .Where(term => !string.IsNullOrWhiteSpace(term));
                List<string> termConditions = [];

                foreach ((string term, int termIndex) in terms.Select((t, idx) => (t, idx)))
                {
                    string searchTerm = term;
                    string paramName = $"@{fieldName}_term_{termIndex}";

                    termConditions.Add($"{fieldName} LIKE {paramName}");
                    parameters.Add(new SqliteParameter(paramName, $"%{searchTerm}%"));
                }

                if (termConditions.Count > 0)
                {
                    conditions.Add($"({string.Join(" OR ", termConditions)})");
                }
            }
            else
            {
                // Single term
                string searchTerm = fieldValue;
                string paramName = $"@{fieldName}_single";

                conditions.Add($"{fieldName} LIKE {paramName}");
                parameters.Add(new SqliteParameter(paramName, $"%{searchTerm}%"));
            }
        }

        if (conditions.Count <= 3) // Only basic filters, no field matches
        {
            return [];
        }

        // Get matching issue records directly
        string query = $"SELECT * FROM issues WHERE {string.Join(" AND ", conditions)} ORDER BY id DESC LIMIT @limit";
        parameters.Add(new SqliteParameter("@limit", limit));

        using SqliteCommand command = new SqliteCommand(query, DatabaseService.Instance.Db);
        command.Parameters.AddRange(parameters.ToArray());

        List<IssueRecord> results = new List<IssueRecord>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            // Map reader to IssueRecord - this might need adjustment based on IssueRecord structure
            IssueRecord? issue = MapReaderToIssueRecord(reader);
            if (issue != null)
            {
                results.Add(issue);
            }
        }

        return results;
    }
    
    /// <summary>
    /// Find issues related through keyword matching using FTS
    /// Returns full IssueRecord objects
    /// </summary>
    private List<IssueRecord> FindKeywordRelatedIssues(string keywords, string excludeKey, int limit)
    {
        using SqliteConnection connection = new SqliteConnection($"Data Source={DatabaseService.Instance.DatabasePath};Mode=ReadOnly");
        connection.Open();

        string[] defaultSearchFields = new[] { "title", "description", "summary", "resolutionDescription" };
        string[] ftsConditions = defaultSearchFields.Select(field => $"{field} MATCH @keywords").ToArray();

        string ftsQuery = $"SELECT * FROM issues_fts WHERE ({string.Join(" OR ", ftsConditions)}) AND key != @exclude_key ORDER BY rank DESC LIMIT @limit";

        using SqliteCommand command = new SqliteCommand(ftsQuery, connection);
        command.Parameters.Add(new SqliteParameter("@keywords", keywords));
        command.Parameters.Add(new SqliteParameter("@exclude_key", excludeKey));
        command.Parameters.Add(new SqliteParameter("@limit", limit));

        List<IssueRecord> results = new List<IssueRecord>();
        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                IssueRecord? issue = MapReaderToIssueRecord(reader);
                if (issue != null)
                {
                    results.Add(issue);
                }
            }
        }
        catch (SqliteException)
        {
            // If FTS5 fails, return empty list
        }

        return results;
    }

    /// <summary>
    /// Map SqliteDataReader to IssueRecord object
    /// This is a simplified mapper - might need adjustment based on actual IssueRecord structure
    /// </summary>
    private IssueRecord? MapReaderToIssueRecord(SqliteDataReader reader)
    {
        try
        {
            // Since we don't have direct access to IssueRecord constructor with reader,
            // we'll use the Key to get the full record via SelectSingle
            string key = reader.GetString("key");
            return IssueRecord.SelectSingle(DatabaseService.Instance.Db, Key: key);
        }
        catch
        {
            return null;
        }
    }
}