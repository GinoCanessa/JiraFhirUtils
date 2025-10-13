using System.Text.Json;
using Shouldly;
using jira_fhir_mcp.Tools;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tests.Tools;

public class ListWorkGroupsToolTests
{
    private readonly ListWorkGroupsTool _tool;

    public ListWorkGroupsToolTests()
    {
        _tool = new ListWorkGroupsTool();
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.ShouldBe("list_work_groups");
    }

    [Fact]
    public void Tool_Should_Have_Correct_Description()
    {
        _tool.Description.ShouldBe("List all unique work groups in the database");
    }

    [Fact]
    public void Tool_Should_Have_No_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(properties["properties"].ToString()!)!;

        argumentProperties.Count.ShouldBe(0); // No arguments required
    }

    [Fact]
    public void Should_Have_No_Required_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;

        // Should not have required arguments since it's an empty array
        if (properties.ContainsKey("required"))
        {
            var requiredArgs = JsonSerializer.Deserialize<string[]>(properties["required"].ToString()!)!;
            requiredArgs.Length.ShouldBe(0);
        }
    }

    [Fact]
    public void Should_Accept_Empty_Arguments_Without_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>();

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // The result may fail due to database access, but should not fail due to argument validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Required arguments are missing or invalid");
        }
    }

    [Fact]
    public void Should_Accept_Null_Arguments_Without_Error()
    {
        // Arrange & Act
        var result = _tool.RunTool(null);

        // Assert
        result.ShouldNotBeNull();
        // The result may fail due to database access, but should not fail due to argument validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Required arguments are missing or invalid");
        }
    }

    [Fact]
    public void Should_Ignore_Extra_Arguments_Without_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["extra_arg"] = JsonSerializer.SerializeToElement("some_value"),
            ["another_arg"] = JsonSerializer.SerializeToElement(123)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // The result may fail due to database access, but should not fail due to argument validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Required arguments are missing or invalid");
        }
    }

    [Fact]
    public void Should_Have_Correct_McpTool_Structure()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;

        // Assert
        mcpTool.Name.ShouldBe("list_work_groups");
        mcpTool.Description.ShouldBe("List all unique work groups in the database");
        mcpTool.InputSchema.ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    public void Should_Have_Proper_InputSchema_Structure()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;
        var schema = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;

        // Assert
        schema.ShouldContainKey("type");
        schema["type"].ToString().ShouldBe("object");
        schema.ShouldContainKey("properties");

        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(schema["properties"].ToString()!)!;
        properties.Count.ShouldBe(0); // No arguments
    }

    [Fact]
    public void Should_Have_Empty_Properties_Object()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;
        var schema = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;

        // Assert
        schema.ShouldContainKey("properties");
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(schema["properties"].ToString()!)!;
        properties.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Have_Object_Type_Schema()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;
        var schema = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;

        // Assert
        schema["type"].ToString().ShouldBe("object");
    }

    [Fact]
    public void Should_Handle_Tool_Execution_Without_Throwing()
    {
        // Arrange & Act
        var result = _tool.RunTool(new Dictionary<string, JsonElement>());

        // Assert
        result.ShouldNotBeNull();
        // Should not throw an exception during execution
        // The result might be an error due to database access, but the tool should handle it gracefully
    }

    [Fact]
    public void Should_Return_CallToolResult_With_Valid_Structure()
    {
        // Arrange & Act
        var result = _tool.RunTool(null);

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldNotBeNull();
        result.Content.Count.ShouldBeGreaterThan(0);

        // First content block should be either TextContentBlock (for errors) or other valid content type
        result.Content[0].ShouldNotBeNull();
    }
}