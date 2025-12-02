using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;
using JiraFhirUtils.Common;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for listing all unique projects in the database
/// </summary>
public class ListProjectsTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "list_projects";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "List all unique projects in the database";

    /// <summary>
    /// Arguments definition for the tool - no arguments required
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [];

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public ListProjectsTool() : base()
    {
    }

    /// <summary>
    /// Execute the list projects tool
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary (unused for this tool)</param>
    /// <returns>CallToolResult with projects list or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        SqliteConnection connection = DatabaseService.Instance.Db;

        string query = $"SELECT DISTINCT {nameof(IssueRecord.ProjectKey)} FROM {IssueRecord.DefaultTableName} ORDER BY {nameof(IssueRecord.ProjectKey)}";
        using SqliteCommand command = new SqliteCommand(query, connection);
        List<string> projects = [];

        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string projectKey = reader.GetString(0);
                if (!string.IsNullOrEmpty(projectKey))
                {
                    projects.Add(projectKey);
                }
            }
        }
        catch (SqliteException ex)
        {
            return CreateErrorResponse($"Database query failed: {ex.Message}");
        }

        var response = new
        {
            total = projects.Count,
            projects = projects
        };

        return CreateSuccessResponse(response);
    }
}