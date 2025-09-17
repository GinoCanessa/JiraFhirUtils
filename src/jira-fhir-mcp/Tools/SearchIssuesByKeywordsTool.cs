using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for searching JIRA issues using SQLite FTS5 full-text search functionality
/// </summary>
public class SearchIssuesByKeywordsTool : BaseJiraTool
{
    private static readonly string[] DefaultSearchFields = ["title", "description", "summary", "resolutionDescription"];

    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "search_issues_by_keywords";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "Search for tickets using SQLite FTS5 testing for keywords in multiple fields";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [
        new ToolArgumentRec("keywords", "string", "Keywords to search for in issues"),
        new ToolArgumentRec("search_fields", "array", "Fields to search in (default: all)"),
        new ToolArgumentRec("limit", "number", "Maximum number of results (default: 20)")
    ];

    /// <summary>
    /// Required argument names for validation
    /// </summary>
    protected override string[] RequiredArguments => ["keywords"];

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public SearchIssuesByKeywordsTool() : base()
    {
    }

    /// <summary>
    /// Override BuildMcpTool to handle the enum values for search_fields
    /// </summary>
    protected override Tool BuildMcpTool()
    {
        Dictionary<string, object> properties = new Dictionary<string, object>();
        List<string> required = new List<string>();

        foreach (ToolArgumentRec arg in Arguments)
        {
            Dictionary<string, object> property = new Dictionary<string, object>
            {
                ["type"] = arg.JsonType,
                ["description"] = arg.Description
            };

            // Special handling for search_fields to add enum values
            if (arg.Name == "search_fields")
            {
                property["items"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = DefaultSearchFields
                };
            }
            else if (arg.Name == "limit")
            {
                property["default"] = 20;
            }

            properties[arg.Name] = property;

            if (RequiredArguments.Contains(arg.Name))
            {
                required.Add(arg.Name);
            }
        }

        Dictionary<string, object> inputSchema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            inputSchema["required"] = required;
        }

        // Convert to JsonElement
        string jsonString = JsonSerializer.Serialize(inputSchema, JsonOptions);
        JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

        return new Tool
        {
            Name = Name,
            Description = Description,
            InputSchema = jsonElement
        };
    }

    /// <summary>
    /// Execute the search issues by keywords tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with search results or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        // Extract arguments
        string keywords = GetArgumentValue<string>(arguments, "keywords");
        string[]? searchFields = GetArgumentValue<string[]?>(arguments, "search_fields");
        int limit = GetArgumentValue<int>(arguments, "limit", 20);

        // Validate arguments
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return CreateErrorResponse("Keywords parameter is required and cannot be empty");
        }

        if (limit <= 0)
        {
            return CreateErrorResponse("Limit must be greater than 0");
        }

        if (limit > 1000)
        {
            return CreateErrorResponse("Limit cannot exceed 1000");
        }

        // Use provided search fields or default to all fields
        string[] fieldsToSearch = searchFields?.Length > 0 ? searchFields : DefaultSearchFields;

        // Validate search fields
        foreach (string field in fieldsToSearch)
        {
            if (!DefaultSearchFields.Contains(field))
            {
                return CreateErrorResponse($"Invalid search field: {field}. Allowed fields: {string.Join(", ", DefaultSearchFields)}");
            }
        }

        try
        {
            List<IssueRecord> issues = SearchIssuesByKeywords(keywords, fieldsToSearch, limit);

            var response = new
            {
                total = issues.Count,
                keywords = keywords,
                search_fields = fieldsToSearch,
                issues = issues
            };

            return CreateSuccessResponse(response);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error searching for issues: {ex.Message}");
        }
    }

    /// <summary>
    /// Search issues using FTS5 full-text search
    /// </summary>
    private List<IssueRecord> SearchIssuesByKeywords(string keywords, string[] searchFields, int limit)
    {
        using SqliteConnection connection = new SqliteConnection($"Data Source={DatabaseService.Instance.DatabasePath};Mode=ReadOnly");
        connection.Open();

        string[] ftsConditions = searchFields.Select(field => $"{field} MATCH @keywords").ToArray();
        string ftsQuery = $"SELECT key FROM issues_fts WHERE ({string.Join(" OR ", ftsConditions)}) ORDER BY rank DESC LIMIT @limit";

        using SqliteCommand command = new SqliteCommand(ftsQuery, connection);
        command.Parameters.Add(new SqliteParameter("@keywords", keywords));
        command.Parameters.Add(new SqliteParameter("@limit", limit));

        List<IssueRecord> results = new List<IssueRecord>();
        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string key = reader.GetString(0);
                IssueRecord? issue = IssueRecord.SelectSingle(DatabaseService.Instance.Db, Key: key);
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
}