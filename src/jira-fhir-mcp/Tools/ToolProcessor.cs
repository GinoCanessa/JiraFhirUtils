using System.Diagnostics;
using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace jira_fhir_mcp.Tools;

public class ToolProcessor
{
    //private ILogger<FhirMcpTools> _logger;
    private McpServerOptions _mcpServerOptions;

    private string CurrentVersion =>
        (FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? "0.0.1") +
        "-" +
        DateTime.UtcNow.ToString("o").Replace(".", string.Empty).Replace("-", string.Empty);

    private readonly List<ITool> _localTools;
    private readonly List<Tool> _mcpTools;
    private readonly Dictionary<string, ITool> _toolDict;

    public ToolProcessor()
    {
        _mcpServerOptions = new McpServerOptions()
        {
            ServerInfo = new Implementation()
            {
                Name = "FHIR Jira MCP Server",
                Version = CurrentVersion,
            }
        };

        _localTools = [
            // new GetStoreList(),
        ];

        _mcpTools = _localTools.Select(t => t.McpTool).ToList();
        _toolDict = _localTools.ToDictionary(t => t.Name);
    }
    
    public ValueTask<ListToolsResult> HandleListToolsRequest(RequestContext<ListToolsRequestParams> request, CancellationToken ct) =>
        new ValueTask<ListToolsResult>(new ListToolsResult() { Tools = _mcpTools, });

    public ValueTask<CallToolResult> HandleCallToolRequest(
        RequestContext<CallToolRequestParams> request,
        CancellationToken ct)
    {
        if (request.Params?.Name == null)
        {
            return ValueTask.FromResult(McpUtils.GetResponse("Tool call without a function name specified!."));
        }

        string fnName = request.Params.Name;

        if (_toolDict.TryGetValue(fnName, out ITool? tool))
        {
            // use the tool's RunTool method
            return ValueTask.FromResult(tool.RunTool(request.Params?.Arguments));
        }

        // fail
        return ValueTask.FromResult(McpUtils.GetResponse($"Unknown tool: {request.Params?.Name}."));
    }
}
