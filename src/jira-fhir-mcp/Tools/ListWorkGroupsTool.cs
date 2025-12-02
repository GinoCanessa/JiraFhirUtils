using System.Text.Json;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Tool for listing all unique work groups in the database
/// </summary>
public class ListWorkGroupsTool : BaseJiraTool
{
    /// <summary>
    /// Tool name exposed to MCP clients
    /// </summary>
    public override string Name => "list_work_groups";

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public override string Description => "List all unique work groups in the database";

    /// <summary>
    /// Arguments definition for the tool - no arguments required
    /// </summary>
    protected override ToolArgumentRec[] Arguments => [];

    /// <summary>
    /// Constructor that accepts DatabaseService
    /// </summary>
    public ListWorkGroupsTool() : base()
    {
    }

    /// <summary>
    /// Execute the list work groups tool
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary (unused for this tool)</param>
    /// <returns>CallToolResult with work groups list or error response</returns>
    protected override CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        SqliteConnection connection = DatabaseService.Instance.Db;

        string query = "SELECT DISTINCT(workGroup) as work_group FROM issues ORDER BY work_group";

        using SqliteCommand command = new SqliteCommand(query, connection);

        List<string> workGroups = new List<string>();

        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string workGroup = reader.GetString(0);
                if (!string.IsNullOrEmpty(workGroup))
                {
                    workGroups.Add(workGroup);
                }
            }
        }
        catch (SqliteException ex)
        {
            return CreateErrorResponse($"Database query failed: {ex.Message}");
        }

        var response = new
        {
            total = workGroups.Count,
            work_groups = workGroups
        };

        return CreateSuccessResponse(response);
    }
}