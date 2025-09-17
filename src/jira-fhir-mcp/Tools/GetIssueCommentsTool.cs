using System.Text.Json;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for retrieving all comments for a specific issue
/// </summary>
public class GetIssueCommentsTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "get_issue_comments";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "Get all comments for a specific issue";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [
        new ToolArgumentRec("issue_key", "string", "The issue key (e.g., FHIR-123)")
    ];

    /// <summary>
    /// Required argument names for validation
    /// </summary>
    protected override string[] RequiredArguments => ["issue_key"];

    /// <summary>
    /// Constructor that initializes the base tool
    /// </summary>
    public GetIssueCommentsTool() : base()
    {
    }

    /// <summary>
    /// Execute the get issue comments tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with comments data or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        string issueKey = GetArgumentValue<string>(arguments, "issue_key");

        if (string.IsNullOrWhiteSpace(issueKey))
        {
            return CreateErrorResponse("issue_key cannot be empty");
        }

        try
        {
            // Get all comments for this issue, ordered by creation date descending
            List<CommentRecord> comments = CommentRecord.SelectList(
                DatabaseService.Instance.Db,
                orderByProperties: [nameof(CommentRecord.CreatedAt)],
                orderByDirection: "DESC",
                IssueKey: issueKey
            );

            // Create response object matching TypeScript format
            var response = new
            {
                issue_key = issueKey,
                total = comments.Count,
                comments
            };

            return CreateSuccessResponse(response);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error getting comments: {ex.Message}");
        }
    }
}