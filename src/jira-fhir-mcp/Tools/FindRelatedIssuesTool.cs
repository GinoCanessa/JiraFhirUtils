using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;
using JiraFhirUtils.SQLiteGenerator;

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

            List<DbIssueKeywordRecord> keywordRecords = DbIssueKeywordRecord.SelectList(
                DatabaseService.Instance.Db,
                IssueId: sourceIssue.Id,
                resultLimit: 5,
                Bm25Score: 0.0,
                Bm25ScoreOperator: JfSQLiteUtils.JfNumericOperatorCodes.GreaterThan,
                orderByProperties: [nameof(DbIssueKeywordRecord.Bm25Score)],
                orderByDirection: "DESC"
            );

            string? keywords = null;

            List<IssueRecord> keywordRelatedIssues = [];
            if (keywordRecords.Count > 0)
            {
                // filter keywords to within 6 points of the top score
                double topScore = keywordRecords[0].Bm25Score ?? 0.0;
                keywordRecords = keywordRecords.Where(kr => kr.Bm25Score > topScore).ToList();
                
                // find the issues related by keyword
                keywordRelatedIssues = findKeywordRelatedIssues(sourceIssue, keywordRecords, limit);
            }

            // Step 6: Combine and deduplicate results
            Dictionary<string, IssueRecord> allMatches = new Dictionary<string, IssueRecord>();

            // // Add field matches
            // foreach (IssueRecord issue in fieldRelatedIssues)
            // {
            //     if (issue.Key != issueKey)
            //     {
            //         allMatches[issue.Key] = issue;
            //     }
            // }

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
    /// Find issues related through keyword matching using BM25 scores
    /// Returns full IssueRecord objects
    /// </summary>
    private List<IssueRecord> findKeywordRelatedIssues(
        IssueRecord sourceIssue,
        List<DbIssueKeywordRecord> keywordRecords,
        int limit)
    {
        try
        {
            // 
            
            // Parse keywords string "keyword1 OR keyword2 OR keyword3"
            List<string> keywordList = ParseKeywords(keywords);
            Dictionary<int, (double score, List<string> matchedKeywords)> issueScores = new();

            // Get all keyword matches with single query using IN clause
            if (keywordList.Count > 0)
            {
                using SqliteCommand command = DatabaseService.Instance.Db.CreateCommand();

                // Build parameterized IN clause
                List<string> parameters = new List<string>();
                for (int i = 0; i < keywordList.Count; i++)
                {
                    string paramName = $"@keyword{i}";
                    parameters.Add(paramName);
                    command.Parameters.Add(new SqliteParameter(paramName, keywordList[i]));
                }

                command.CommandText = $@"
                    SELECT IssueId, Keyword, Bm25Score
                    FROM issue_keywords
                    WHERE Keyword IN ({string.Join(", ", parameters)})
                      AND Bm25Score IS NOT NULL
                      AND Bm25Score > 0
                    ORDER BY Bm25Score DESC";

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int issueId = reader.GetInt32("IssueId");
                    string keyword = reader.GetString("Keyword");
                    double bm25Score = reader.GetDouble("Bm25Score");

                    // Initialize score tracking for new issues
                    if (!issueScores.ContainsKey(issueId))
                    {
                        issueScores[issueId] = (0, new List<string>());
                    }

                    // Add BM25 score and track matched keyword
                    (double score, List<string> matchedKeywords) current = issueScores[issueId];
                    issueScores[issueId] = (
                        current.score + bm25Score,
                        current.matchedKeywords.Concat(new[] { keyword }).ToList()
                    );
                }
            }

            // Get top N issue IDs by score, excluding the source issue
            List<int> topIssueIds = issueScores
                .OrderByDescending(kvp => kvp.Value.score)
                .Take(limit * 2) // Take more to account for filtering
                .Select(kvp => kvp.Key)
                .ToList();

            // Batch load full issue records and apply exclusion filter
            List<IssueRecord> results = new();
            if (topIssueIds.Count > 0)
            {
                // Create parameterized IN clause for batch loading
                using SqliteCommand command = DatabaseService.Instance.Db.CreateCommand();
                List<string> parameters = new List<string>();
                for (int i = 0; i < topIssueIds.Count; i++)
                {
                    string paramName = $"@issueId{i}";
                    parameters.Add(paramName);
                    command.Parameters.Add(new SqliteParameter(paramName, topIssueIds[i]));
                }

                command.CommandText = $@"
                    SELECT * FROM issues
                    WHERE Id IN ({string.Join(", ", parameters)})
                    ORDER BY
                        CASE Id {string.Join(" ", topIssueIds.Select((id, idx) => $"WHEN {id} THEN {idx}"))} END";

                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read() && results.Count < limit)
                {
                    IssueRecord? issue = MapReaderToIssueRecord(reader);
                    if (issue != null && issue.Key != excludeKey)
                    {
                        results.Add(issue);
                    }
                }
            }

            return results;
        }
        catch
        {
            // If BM25 search fails, return empty list
            return new List<IssueRecord>();
        }
    }

    /// <summary>
    /// Parse keywords string "keyword1 OR keyword2 OR keyword3" into individual keywords
    /// </summary>
    private List<string> ParseKeywords(string keywordString)
    {
        return keywordString
            .Split(new[] { " OR " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();
    }

    /// <summary>
    /// Map SqliteDataReader to IssueRecord object directly from database fields
    /// </summary>
    private IssueRecord? MapReaderToIssueRecord(SqliteDataReader reader)
    {
        try
        {
            return new IssueRecord
            {
                Id = reader.GetInt32("Id"),
                Key = reader.GetString("Key"),
                Title = reader.GetString("Title"),
                IssueUrl = reader.GetString("IssueUrl"),
                ProjectId = reader.GetInt32("ProjectId"),
                ProjectKey = reader.GetString("ProjectKey"),
                Description = reader.GetString("Description"),
                Summary = reader.IsDBNull("Summary") ? null : reader.GetString("Summary"),
                Type = reader.GetString("Type"),
                TypeId = reader.GetInt32("TypeId"),
                Priority = reader.IsDBNull("Priority") ? null : reader.GetString("Priority"),
                PriorityId = reader.IsDBNull("PriorityId") ? null : reader.GetInt32("PriorityId"),
                Status = reader.IsDBNull("Status") ? null : reader.GetString("Status"),
                StatusId = reader.GetInt32("StatusId"),
                Resolution = reader.GetString("Resolution"),
                ResolutionId = reader.GetInt32("ResolutionId"),
                Assignee = reader.IsDBNull("Assignee") ? null : reader.GetString("Assignee"),
                Reporter = reader.IsDBNull("Reporter") ? null : reader.GetString("Reporter"),
                CreatedAt = reader.IsDBNull("CreatedAt") ? null : reader.GetDateTime("CreatedAt"),
                UpdatedAt = reader.IsDBNull("UpdatedAt") ? null : reader.GetDateTime("UpdatedAt"),
                ResolvedAt = reader.IsDBNull("ResolvedAt") ? null : reader.GetDateTime("ResolvedAt"),
                Watches = reader.IsDBNull("Watches") ? null : reader.GetString("Watches"),
                Specification = reader.IsDBNull("Specification") ? null : reader.GetString("Specification"),
                AppliedForVersion = reader.IsDBNull("AppliedForVersion") ? null : reader.GetString("AppliedForVersion"),
                ChangeCategory = reader.IsDBNull("ChangeCategory") ? null : reader.GetString("ChangeCategory"),
                ChangeImpact = reader.IsDBNull("ChangeImpact") ? null : reader.GetString("ChangeImpact"),
                DuplicateIssue = reader.IsDBNull("DuplicateIssue") ? null : reader.GetString("DuplicateIssue"),
                DuplicateVotedIssue = reader.IsDBNull("DuplicateVotedIssue") ? null : reader.GetString("DuplicateVotedIssue"),
                Grouping = reader.IsDBNull("Grouping") ? null : reader.GetString("Grouping"),
                RaisedInVersion = reader.IsDBNull("RaisedInVersion") ? null : reader.GetString("RaisedInVersion"),
                RelatedIssues = reader.IsDBNull("RelatedIssues") ? null : reader.GetString("RelatedIssues"),
                RelatedArtifacts = reader.IsDBNull("RelatedArtifacts") ? null : reader.GetString("RelatedArtifacts"),
                RelatedPages = reader.IsDBNull("RelatedPages") ? null : reader.GetString("RelatedPages"),
                RelatedSections = reader.IsDBNull("RelatedSections") ? null : reader.GetString("RelatedSections"),
                RelatedUrl = reader.IsDBNull("RelatedUrl") ? null : reader.GetString("RelatedUrl"),
                ResolutionDescription = reader.IsDBNull("ResolutionDescription") ? null : reader.GetString("ResolutionDescription"),
                VoteDate = reader.IsDBNull("VoteDate") ? null : reader.GetDateTime("VoteDate"),
                Vote = reader.IsDBNull("Vote") ? null : reader.GetString("Vote"),
                BlockVote = reader.IsDBNull("BlockVote") ? null : reader.GetString("BlockVote"),
                WorkGroup = reader.IsDBNull("WorkGroup") ? null : reader.GetString("WorkGroup"),
                SelectedBallot = reader.IsDBNull("SelectedBallot") ? null : reader.GetString("SelectedBallot"),
                RequestInPerson = reader.IsDBNull("RequestInPerson") ? null : reader.GetString("RequestInPerson"),
                AiIssueSummary = reader.IsDBNull("AiIssueSummary") ? null : reader.GetString("AiIssueSummary"),
                AiCommentSummary = reader.IsDBNull("AiCommentSummary") ? null : reader.GetString("AiCommentSummary"),
                AiResolutionSummary = reader.IsDBNull("AiResolutionSummary") ? null : reader.GetString("AiResolutionSummary")
            };
        }
        catch
        {
            return null;
        }
    }
}