using System.Text.Json;
using Shouldly;
using jira_fhir_mcp.Tools;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tests.Tools;

public class FindRelatedIssuesToolTests
{
    private readonly FindRelatedIssuesTool _tool;

    public FindRelatedIssuesToolTests()
    {
        _tool = new FindRelatedIssuesTool();
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.ShouldBe("find_related_issues");
    }

    [Fact]
    public void Tool_Should_Have_Correct_Description()
    {
        _tool.Description.ShouldBe("Find and return full issue records related to a specific JIRA issue through explicit links and keyword similarity. Returns complete issue objects for both explicitly linked issues and keyword-related issues.");
    }

    [Fact]
    public void Tool_Should_Have_All_Expected_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(properties["properties"].ToString()!)!;

        var expectedArguments = new[] { "issue_key", "limit" };

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

        // Check number arguments
        argumentProperties["limit"]["type"].ToString().ShouldBe("number");
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Should_Reject_Invalid_Limit_Values(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("TEST-123"),
            ["limit"] = JsonSerializer.SerializeToElement(limit)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);

        if (limit <= 0)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            ((TextContentBlock)result.Content[0]).Text.ShouldContain("Limit must be greater than 0");
        }
        else if (limit > 100)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            ((TextContentBlock)result.Content[0]).Text.ShouldContain("Limit cannot exceed 100");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Should_Not_Reject_Valid_Limit_Values_Due_To_Validation(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("TEST-123"),
            ["limit"] = JsonSerializer.SerializeToElement(limit)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        // Should not fail due to limit validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Limit must be greater than 0");
            errorText.ShouldNotContain("Limit cannot exceed 100");
        }
    }

    [Fact]
    public void Should_Accept_Valid_Issue_Key_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("TEST-123")
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
    public void Should_Handle_Default_Limit_When_Not_Provided()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("TEST-123")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to missing limit argument since it has a default
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
        mcpTool.Name.ShouldBe("find_related_issues");
        mcpTool.Description.ShouldBe("Find and return full issue records related to a specific JIRA issue through explicit links and keyword similarity. Returns complete issue objects for both explicitly linked issues and keyword-related issues.");
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
        properties.Count.ShouldBe(2); // issue_key and limit
    }

    [Fact]
    public void Should_Handle_Both_Arguments_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["issue_key"] = JsonSerializer.SerializeToElement("TEST-123"),
            ["limit"] = JsonSerializer.SerializeToElement(25)
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
            errorText.ShouldNotContain("Limit must be greater than 0");
            errorText.ShouldNotContain("Limit cannot exceed 100");
        }
    }
}