using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for finding and listing issues related to a specific JIRA issue through explicit links and keyword similarity
/// </summary>
public class ListRelatedIssuesTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "list_related_issues";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "Find and list issues related to a specific JIRA issue through explicit links and keyword similarity. Returns both explicitly linked issues and keyword-related issues.";

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
    public ListRelatedIssuesTool() : base()
    {
    }

    /// <summary>
    /// Execute the list related issues tool with provided arguments
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

            // Step 2: Get explicit linked issues from RelatedIssues property
            List<string> linkedIssues = [];
            if (!string.IsNullOrWhiteSpace(sourceIssue.RelatedIssues))
            {
                linkedIssues = sourceIssue.RelatedIssues
                    .Split(',')
                    .Select(issue => issue.Trim())
                    .Where(issue => !string.IsNullOrWhiteSpace(issue))
                    .ToList();
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
            List<string> fieldRelatedIssues = FindFieldRelatedIssues(sourceIssue, issueKey, limit);

            // Step 5: Query related issues using keyword matching (FTS)
            List<string> keywordRelatedIssues = [];
            if (!string.IsNullOrWhiteSpace(keywords))
            {
                keywordRelatedIssues = FindKeywordRelatedIssues(keywords, issueKey, limit);
            }

            // Step 6: Combine and deduplicate results
            HashSet<string> allMatches = new HashSet<string>();
            allMatches.UnionWith(fieldRelatedIssues);
            allMatches.UnionWith(keywordRelatedIssues);

            // Remove the source issue itself and limit results
            List<string> finalMatches = allMatches
                .Where(key => key != issueKey)
                .Take(limit)
                .ToList();

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
    /// </summary>
    private List<string> FindFieldRelatedIssues(IssueRecord sourceIssue, string excludeKey, int limit)
    {
        using SqliteConnection connection = new SqliteConnection($"Data Source={DatabaseService.Instance.DatabasePath};Mode=ReadOnly");
        connection.Open();

        List<string> conditions = new List<string>();
        List<SqliteParameter> parameters = new List<SqliteParameter>();

        // Match project_key and work_group
        conditions.Add("project_key = @project_key");
        conditions.Add("workGroup = @work_group");
        conditions.Add("key != @exclude_key");

        parameters.Add(new SqliteParameter("@project_key", sourceIssue.ProjectKey));
        parameters.Add(new SqliteParameter("@work_group", sourceIssue.WorkGroup));
        parameters.Add(new SqliteParameter("@exclude_key", excludeKey));

        // Add relatedUrl, relatedArtifacts, relatedPages if present
        string[] fieldNames = new[] { "relatedUrl", "relatedArtifacts", "relatedPages" };
        string?[] fieldValues = new[] { sourceIssue.RelatedUrl, sourceIssue.RelatedArtifacts, sourceIssue.RelatedPages };

        for (int i = 0; i < fieldNames.Length; i++)
        {
            string fieldName = fieldNames[i];
            string? fieldValue = fieldValues[i];

            if (string.IsNullOrWhiteSpace(fieldValue))
                continue;

            if (fieldValue.Contains(','))
            {
                // Multiple terms separated by commas
                IEnumerable<string> terms = fieldValue.Split(',').Select(term => term.Trim()).Where(term => !string.IsNullOrWhiteSpace(term));
                List<string> termConditions = new List<string>();

                foreach (var termInfo in terms.Select((t, idx) => new { term = t, termIndex = idx }))
                {
                    string term = termInfo.term;
                    int termIndex = termInfo.termIndex;
                    string searchTerm = ProcessFieldTerm(fieldName, term);
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
                string searchTerm = ProcessFieldTerm(fieldName, fieldValue);
                string paramName = $"@{fieldName}_single";

                conditions.Add($"{fieldName} LIKE {paramName}");
                parameters.Add(new SqliteParameter(paramName, $"%{searchTerm}%"));
            }
        }

        if (conditions.Count <= 3) // Only basic filters, no field matches
        {
            return [];
        }

        string query = $"SELECT key FROM issues WHERE {string.Join(" AND ", conditions)} ORDER BY id DESC LIMIT @limit";
        parameters.Add(new SqliteParameter("@limit", limit));

        using SqliteCommand command = new SqliteCommand(query, connection);
        command.Parameters.AddRange(parameters.ToArray());

        List<string> results = new List<string>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    /// <summary>
    /// Process field terms, especially extracting filenames from URLs
    /// </summary>
    private static string ProcessFieldTerm(string fieldName, string term)
    {
        if (fieldName == "relatedUrl" && Uri.TryCreate(term, UriKind.Absolute, out Uri? uri))
        {
            // Extract filename from URL path
            string fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                // Remove file extension
                fileName = Path.GetFileNameWithoutExtension(fileName);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }
        }

        return term;
    }

    /// <summary>
    /// Find issues related through keyword matching using FTS
    /// </summary>
    private List<string> FindKeywordRelatedIssues(string keywords, string excludeKey, int limit)
    {
        using SqliteConnection connection = new SqliteConnection($"Data Source={DatabaseService.Instance.DatabasePath};Mode=ReadOnly");
        connection.Open();

        string[] defaultSearchFields = new[] { "title", "description", "summary", "resolutionDescription" };
        string[] ftsConditions = defaultSearchFields.Select(field => $"{field} MATCH @keywords").ToArray();

        string ftsQuery = $"SELECT key FROM issues_fts WHERE ({string.Join(" OR ", ftsConditions)}) AND key != @exclude_key ORDER BY rank DESC LIMIT @limit";

        using SqliteCommand command = new SqliteCommand(ftsQuery, connection);
        command.Parameters.Add(new SqliteParameter("@keywords", keywords));
        command.Parameters.Add(new SqliteParameter("@exclude_key", excludeKey));
        command.Parameters.Add(new SqliteParameter("@limit", limit));

        List<string> results = new List<string>();
        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }
        }
        catch (SqliteException)
        {
            // If FTS5 fails, return empty list
        }

        return results;
    }
}