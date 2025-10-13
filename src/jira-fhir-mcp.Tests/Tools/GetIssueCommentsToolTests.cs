using System.Text.Json;
using Shouldly;
using jira_fhir_mcp.Tools;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tests.Tools;

public class GetIssueCommentsToolTests
{
    private readonly GetIssueCommentsTool _tool;

    public GetIssueCommentsToolTests()
    {
        _tool = new GetIssueCommentsTool();
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.ShouldBe("get_issue_comments");
    }

    [Fact]
    public void Tool_Should_Have_Correct_Description()
    {
        _tool.Description.ShouldBe("Get all comments for a specific issue");
    }

    [Fact]
    public void Tool_Should_Have_All_Expected_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(properties["properties"].ToString()!)!;

        var expectedArguments = new[] { "issue_key" };

        foreach (var expectedArg in expectedArguments)
        {
            argumentProperties.ShouldContainKey(expectedArg);
        }
    }

    [Fact]
    public void Should_Have_Required_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;

        properties.ShouldContainKey("required");
        var requiredArgs = JsonSerializer.Deserialize<string[]>(properties["required"].ToString()!)!;
        requiredArgs.ShouldContain("issue_key");
    }

    [Fact]
    public void Should_Have_Correct_Argument_Types()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        // Check string arguments
        argumentProperties["issue_key"]["type"].ToString().ShouldBe("string");
    }

    [Fact]
    public void Should_Reject_Missing_Required_Issue_Key()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>();

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Required arguments are missing or invalid");
    }

    [Fact]
    public void Should_Reject_Null_Issue_Key()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement((string?)null)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Required arguments are missing or invalid");
    }

    [Fact]
    public void Should_Accept_Valid_Issue_Key_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("FHIR-123")
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

    [Theory]
    [InlineData("FHIR-123")]
    [InlineData("PROJECT-456")]
    [InlineData("TEST-1")]
    [InlineData("EXAMPLE-999")]
    public void Should_Accept_Various_Issue_Key_Formats_Without_Validation_Error(string issueKey)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement(issueKey)
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
        mcpTool.Name.ShouldBe("get_issue_comments");
        mcpTool.Description.ShouldBe("Get all comments for a specific issue");
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
        properties.Count.ShouldBe(1); // issue_key only
    }

    [Fact]
    public void Should_Have_Issue_Key_Argument_With_Correct_Description()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        argumentProperties["issue_key"].ShouldContainKey("description");
        argumentProperties["issue_key"]["description"].ToString().ShouldBe("The issue key (e.g., FHIR-123)");
    }

    [Fact]
    public void Should_Handle_Null_Arguments_Dictionary()
    {
        // Arrange & Act
        var result = _tool.RunTool(null);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Required arguments are missing or invalid");
    }

    [Fact]
    public void Should_Handle_Empty_Arguments_Dictionary()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>();

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Required arguments are missing or invalid");
    }
}