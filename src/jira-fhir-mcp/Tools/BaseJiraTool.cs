using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Protocol;
using jira_fhir_mcp.Services;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Base abstract class for JIRA-related MCP tools
/// Provides common database access patterns, error handling, and JSON serialization helpers
/// </summary>
public abstract class BaseJiraTool : ITool
{
    /// <summary>
    /// Database service for executing queries
    /// </summary>
    protected readonly DatabaseService _databaseService;

    /// <summary>
    /// JSON serializer options with camelCase naming policy
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initialize base tool with database service
    /// </summary>
    /// <param name="databaseService">Database service for data access</param>
    protected BaseJiraTool(DatabaseService databaseService)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    }

    /// <summary>
    /// Tool name as exposed to MCP clients
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Tool arguments definition for MCP schema
    /// </summary>
    protected abstract IEnumerable<ToolArgumentRec> Arguments { get; }

    /// <summary>
    /// Required argument names for validation
    /// </summary>
    protected virtual IEnumerable<string> RequiredArguments => [];

    /// <summary>
    /// MCP Tool definition built from Name, Description, and Arguments
    /// </summary>
    public virtual Tool McpTool => BuildMcpTool();

    /// <summary>
    /// Execute the tool with provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments dictionary</param>
    /// <returns>CallToolResult with success or error response</returns>
    public CallToolResult RunTool(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        try
        {
            // Validate required arguments
            if (!ValidateArguments(arguments))
            {
                return CreateErrorResponse("Required arguments are missing or invalid");
            }

            // Execute the tool implementation
            return ExecuteInternal(arguments);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal tool execution logic - implemented by derived classes
    /// </summary>
    /// <param name="arguments">Validated arguments dictionary</param>
    /// <returns>CallToolResult with tool-specific response</returns>
    protected abstract CallToolResult ExecuteInternal(IReadOnlyDictionary<string, JsonElement>? arguments);

    /// <summary>
    /// Build MCP Tool definition from tool metadata
    /// </summary>
    /// <returns>Tool definition for MCP registration</returns>
    protected virtual Tool BuildMcpTool()
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var arg in Arguments)
        {
            var property = new Dictionary<string, object>
            {
                ["type"] = arg.JsonType,
                ["description"] = arg.Description
            };

            properties[arg.Name] = property;

            if (RequiredArguments.Contains(arg.Name))
            {
                required.Add(arg.Name);
            }
        }

        var inputSchema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            inputSchema["required"] = required;
        }

        // Convert to JsonElement
        var jsonString = JsonSerializer.Serialize(inputSchema, JsonOptions);
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

        return new Tool
        {
            Name = Name,
            Description = Description,
            InputSchema = jsonElement
        };
    }

    /// <summary>
    /// Validate that all required arguments are present and not null
    /// </summary>
    /// <param name="arguments">Arguments to validate</param>
    /// <returns>True if validation passes</returns>
    protected virtual bool ValidateArguments(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (arguments == null && RequiredArguments.Any())
        {
            return false;
        }

        foreach (var requiredArg in RequiredArguments)
        {
            if (arguments == null ||
                !arguments.ContainsKey(requiredArg) ||
                arguments[requiredArg].ValueKind == JsonValueKind.Null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Safely get typed argument value from dictionary
    /// </summary>
    /// <typeparam name="T">Target type</typeparam>
    /// <param name="arguments">Arguments dictionary</param>
    /// <param name="key">Argument key</param>
    /// <param name="defaultValue">Default value if not found or conversion fails</param>
    /// <returns>Typed argument value or default</returns>
    protected T GetArgumentValue<T>(IReadOnlyDictionary<string, JsonElement>? arguments, string key, T defaultValue = default!)
    {
        if (arguments == null || !arguments.TryGetValue(key, out JsonElement element))
        {
            return defaultValue;
        }

        try
        {
            return element.ValueKind == JsonValueKind.Null
                ? defaultValue
                : JsonSerializer.Deserialize<T>(element, JsonOptions)!;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Execute database query with error handling
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>Query results or null on error</returns>
    protected async Task<List<Dictionary<string, object?>>?> ExecuteQuerySafeAsync(string query, params SqliteParameter[] parameters)
    {
        try
        {
            return await _databaseService.ExecuteQueryAsync(query, parameters);
        }
        catch (Exception ex)
        {
            // Log error if needed, but return null to indicate failure
            Console.Error.WriteLine($"Database query failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Execute scalar database query with error handling
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>Scalar result or null on error</returns>
    protected async Task<object?> ExecuteScalarSafeAsync(string query, params SqliteParameter[] parameters)
    {
        try
        {
            return await _databaseService.ExecuteScalarAsync(query, parameters);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database scalar query failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create successful CallToolResult with JSON content
    /// </summary>
    /// <param name="data">Data to serialize as JSON response</param>
    /// <returns>Success CallToolResult</returns>
    protected CallToolResult CreateSuccessResponse(object data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        return McpUtils.GetResponse(json);
    }

    /// <summary>
    /// Create error CallToolResult with error message
    /// </summary>
    /// <param name="message">Error message</param>
    /// <returns>Error CallToolResult</returns>
    protected CallToolResult CreateErrorResponse(string message)
    {
        return new CallToolResult
        {
            Content = [
                new TextContentBlock
                {
                    Type = "text",
                    Text = message
                }
            ],
            IsError = true
        };
    }

    /// <summary>
    /// Build dynamic WHERE clause for database queries
    /// </summary>
    /// <param name="conditions">List of WHERE conditions</param>
    /// <returns>WHERE clause string or empty string</returns>
    protected static string BuildWhereClause(IEnumerable<string> conditions)
    {
        var conditionList = conditions.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        return conditionList.Count > 0 ? " WHERE " + string.Join(" AND ", conditionList) : "";
    }

    /// <summary>
    /// Create pagination parameters for LIMIT/OFFSET queries
    /// </summary>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="offset">Number of results to skip</param>
    /// <returns>Array of SqliteParameter for pagination</returns>
    protected static SqliteParameter[] CreatePaginationParameters(int limit, int offset)
    {
        return [
            DatabaseService.CreateParameter("@limit", limit),
            DatabaseService.CreateParameter("@offset", offset)
        ];
    }
}