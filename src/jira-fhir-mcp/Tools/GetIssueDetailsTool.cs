using System.Text.Json;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for getting detailed information about a specific JIRA issue
/// </summary>
public class GetIssueDetailsTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "get_issue_details";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "Get detailed information about a specific JIRA issue including all fields and comment count";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [
        new ToolArgumentRec("issue_key", "string", "The JIRA issue key to fetch details for")
    ];

    /// <summary>
    /// Required argument names for validation
    /// </summary>
    protected override string[] RequiredArguments => ["issue_key"];

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public GetIssueDetailsTool() : base()
    {
    }

    /// <summary>
    /// Execute the get issue details tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with issue details or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        string issueKey = GetArgumentValue<string>(arguments, "issue_key");

        if (string.IsNullOrWhiteSpace(issueKey))
        {
            return CreateErrorResponse("Issue key cannot be empty");
        }

        // Get the issue from the database
        IssueRecord? issue = IssueRecord.SelectSingle(
            DatabaseService.Instance.Db,
            Key: issueKey
        );

        if (issue == null)
        {
            return CreateErrorResponse($"Issue {issueKey} not found");
        }

        // Get comment count for this issue
        int commentCount = CommentRecord.SelectCount(
            DatabaseService.Instance.Db,
            IssueKey: issueKey
        );

        // Create response object with issue data plus comment count
        var response = new
        {
            issue = issue,
            comment_count = commentCount
        };

        return CreateSuccessResponse(response);
    }
}