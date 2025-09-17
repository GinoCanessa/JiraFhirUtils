using System.Data;
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
    public override string Description => "List JIRA issues with comprehensive filtering options including project, workgroup, status, type, priority, assignee, reporter, and more. Supports pagination and sorting.";

    /// <summary>
    /// Arguments definition for the tool
    /// </summary>
    protected override IEnumerable<ToolArgumentRec> Arguments => new[]
    {
        new ToolArgumentRec("project", "string", "Filter by project key"),
        new ToolArgumentRec("workgroup", "string", "Filter by work group"),
        new ToolArgumentRec("resolution", "string", "Filter by resolution"),
        new ToolArgumentRec("status", "string", "Filter by status"),
        new ToolArgumentRec("assignee", "string", "Filter by assignee"),
        new ToolArgumentRec("type", "string", "Filter by issue type"),
        new ToolArgumentRec("priority", "string", "Filter by priority"),
        new ToolArgumentRec("reporter", "string", "Filter by reporter"),
        new ToolArgumentRec("specification", "string", "Filter by specification"),
        new ToolArgumentRec("vote", "string", "Filter by vote status"),
        new ToolArgumentRec("grouping", "string", "Filter by grouping"),
        new ToolArgumentRec("limit", "number", "Maximum number of results (default: 50)"),
        new ToolArgumentRec("offset", "number", "Offset for pagination (default: 0)"),
        // new ToolArgumentRec("created_after", "string", "Filter by creation date (ISO 8601)"),
        // new ToolArgumentRec("created_before", "string", "Filter by creation date (ISO 8601)"),
        // new ToolArgumentRec("updated_after", "string", "Filter by update date (ISO 8601)"),
        // new ToolArgumentRec("updated_before", "string", "Filter by update date (ISO 8601)"),
        // new ToolArgumentRec("resolved_after", "string", "Filter by resolution date (ISO 8601)"),
        // new ToolArgumentRec("resolved_before", "string", "Filter by resolution date (ISO 8601)"),
        new ToolArgumentRec("sort", "string", "Sort field (id, key, created, updated, priority)"),
        new ToolArgumentRec("order", "string", "Sort order (asc or desc, default: desc)")
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
        // Extract pagination parameters separately
        int? limit = GetArgumentValue<int?>(arguments, "limit", null);
        int? offset = GetArgumentValue<int?>(arguments, "offset", null);

        // Validate pagination parameters
        if (limit is <= 0)
        {
            return CreateErrorResponse("Limit must be greater than 0");
        }

        if (limit is > 1000)
        {
            return CreateErrorResponse("Limit cannot exceed 1000");
        }

        if (offset is < 0)
        {
            return CreateErrorResponse("Offset cannot be negative");
        }

        // Extract and validate sort parameters
        string? sortField = GetArgumentValue<string?>(arguments, "sort", null);
        string? sortOrder = GetArgumentValue<string?>(arguments, "order", "desc");

        // Normalize and validate sort order
        sortOrder = sortOrder?.ToLower() switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => "DESC",
        };

        // Map sort field to property name
        string[] orderByProperties = sortField?.ToLower() switch
        {
            "key" => [nameof(IssueRecord.Key)],
            "created" => [nameof(IssueRecord.CreatedAt)],
            "updated" => [nameof(IssueRecord.UpdatedAt)],
            "priority" => [nameof(IssueRecord.PriorityId)],
            _ => [nameof(IssueRecord.Id)]
        };
        
        List<IssueRecord> issues = IssueRecord.SelectList(
            DatabaseService.Instance.Db,
            resultLimit: limit,
            resultOffset: offset,
            orderByProperties: orderByProperties,
            orderByDirection: sortOrder,
            ProjectKey: GetArgumentValue<string?>(arguments, "project"),
            WorkGroup: GetArgumentValue<string?>(arguments, "workgroup"),
            Resolution: GetArgumentValue<string?>(arguments, "resolution"),
            Status: GetArgumentValue<string?>(arguments, "status"),
            Assignee: GetArgumentValue<string?>(arguments, "assignee"),
            Type: GetArgumentValue<string?>(arguments, "type"),
            Priority: GetArgumentValue<string?>(arguments, "priority"),
            Reporter: GetArgumentValue<string?>(arguments, "reporter"),
            Specification: GetArgumentValue<string?>(arguments, "specification"),
            Vote: GetArgumentValue<string?>(arguments, "vote"),
            Grouping: GetArgumentValue<string?>(arguments, "grouping")
        );

        int totalCount = IssueRecord.SelectCount(
            DatabaseService.Instance.Db,
            ProjectKey: GetArgumentValue<string?>(arguments, "project"),
            WorkGroup: GetArgumentValue<string?>(arguments, "workgroup"),
            Resolution: GetArgumentValue<string?>(arguments, "resolution"),
            Status: GetArgumentValue<string?>(arguments, "status"),
            Assignee: GetArgumentValue<string?>(arguments, "assignee"),
            Type: GetArgumentValue<string?>(arguments, "type"),
            Priority: GetArgumentValue<string?>(arguments, "priority"),
            Reporter: GetArgumentValue<string?>(arguments, "reporter"),
            Specification: GetArgumentValue<string?>(arguments, "specification"),
            Vote: GetArgumentValue<string?>(arguments, "vote"),
            Grouping: GetArgumentValue<string?>(arguments, "grouping")
        );
        
        // Create enhanced response object
        var response = new
        {
            total = totalCount,
            returned = issues.Count,
            offset = offset ?? 0,
            limit = limit ?? 0,
            hasMore = (offset ?? 0) + issues.Count < totalCount,
            issues
        };

        return CreateSuccessResponse(response);
    }
}