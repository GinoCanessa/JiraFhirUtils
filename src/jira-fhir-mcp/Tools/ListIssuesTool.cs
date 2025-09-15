using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for listing issues with optional filtering by project_key, work_group, resolution, status, and assignee
/// </summary>
public class ListIssuesTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "list_issues";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "List issues filtered by project_key, work_group, resolution, status, and/or assignee";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override IEnumerable<ToolArgumentRec> Arguments => new[]
    {
        new ToolArgumentRec("project_key", "string", "Filter by project key"),
        new ToolArgumentRec("work_group", "string", "Filter by work group"),
        new ToolArgumentRec("resolution", "string", "Filter by resolution"),
        new ToolArgumentRec("status", "string", "Filter by status"),
        new ToolArgumentRec("assignee", "string", "Filter by assignee"),
        new ToolArgumentRec("limit", "number", "Maximum number of results (default: 50)"),
        new ToolArgumentRec("offset", "number", "Offset for pagination (default: 0)")
    };

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public ListIssuesTool() : base()
    {
    }

    /// <summary>
    /// Execute the list issues tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with issue list or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        // Extract arguments with defaults
        string? projectKey = GetArgumentValue<string?>(arguments, "project_key", null);
        string? workGroup = GetArgumentValue<string?>(arguments, "work_group", null);
        string? resolution = GetArgumentValue<string?>(arguments, "resolution", null);
        string? status = GetArgumentValue<string?>(arguments, "status", null);
        string? assignee = GetArgumentValue<string?>(arguments, "assignee", null);
        int? limit = GetArgumentValue<int?>(arguments, "limit", null);
        int? offset = GetArgumentValue<int?>(arguments, "offset", null);

        // Use IssueRecord.SelectList with filtering parameters
        List<IssueRecord> issues = IssueRecord.SelectList(
            DatabaseService.Instance.Db,
            resultLimit: limit,
            resultOffset: offset,
            orderByProperties: [nameof(IssueRecord.Id)],
            orderByDirection: "DESC",
            ProjectKey: projectKey,
            WorkGroup: workGroup,
            Resolution: resolution,
            Status: status,
            Assignee: assignee
        );

        // Create response object matching TypeScript structure
        var response = new
        {
            total = issues.Count,
            offset,
            issues
        };

        return CreateSuccessResponse(response);
    }
}