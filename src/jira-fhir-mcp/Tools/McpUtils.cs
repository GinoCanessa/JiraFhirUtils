using System.Runtime.CompilerServices;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tools;

public static class McpUtils
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallToolResult GetResponse(IEnumerable<string> responses) => new CallToolResult()
    {
        Content = responses.Select(r => new TextContentBlock()
        {
            
            Text = r,
            Type = "text",
        }).ToList<ContentBlock>(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallToolResult GetResponse(string response) => new CallToolResult()
    {
        Content = [
            new TextContentBlock
            {
                Text = response,
                Type = "text",
            }
        ],
    };
}