using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tools;

/// <summary>
/// Represents a tool argument with its metadata for MCP tool registration.
/// </summary>
/// <param name="Name">The name of the argument.</param>
/// <param name="JsonType">The JSON type of the argument (e.g., "string", "number", "boolean").</param>
/// <param name="Description">A description of what the argument is used for.</param>
public record struct ToolArgumentRec(
    string Name,
    string JsonType,
    string Description);

/// <summary>
/// Interface for FHIR Candle Model Context Protocol (MCP) tools.
/// Provides a contract for tools that can be exposed through the MCP interface.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the name of the tool as it will be exposed in the MCP interface.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the MCP Tool definition that describes this tool's interface.
    /// </summary>
    Tool McpTool { get; }

    /// <summary>
    /// Executes the tool with the provided arguments and context.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool, if any.</param>
    /// <returns>A <see cref="CallToolResult"/>The result of the tool execution.</returns>
    CallToolResult RunTool(IReadOnlyDictionary<string, JsonElement>? arguments);
}
