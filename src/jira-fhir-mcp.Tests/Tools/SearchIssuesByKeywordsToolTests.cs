using System.Text.Json;
using Shouldly;
using jira_fhir_mcp.Tools;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tests.Tools;

public class SearchIssuesByKeywordsToolTests
{
    private readonly SearchIssuesByKeywordsTool _tool;

    public SearchIssuesByKeywordsToolTests()
    {
        _tool = new SearchIssuesByKeywordsTool();
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.ShouldBe("search_issues_by_keywords");
    }

    [Fact]
    public void Tool_Should_Have_Correct_Description()
    {
        _tool.Description.ShouldBe("Search for tickets using SQLite FTS5 testing for keywords in multiple fields");
    }

    [Fact]
    public void Tool_Should_Have_All_Expected_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(properties["properties"].ToString()!)!;

        var expectedArguments = new[] { "keywords", "search_fields", "limit" };

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
        requiredArgs.ShouldContain("keywords");
    }

    [Fact]
    public void Should_Have_Correct_Argument_Types()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        // Check string arguments
        argumentProperties["keywords"]["type"].ToString().ShouldBe("string");

        // Check array arguments
        argumentProperties["search_fields"]["type"].ToString().ShouldBe("array");

        // Check number arguments
        argumentProperties["limit"]["type"].ToString().ShouldBe("number");
    }

    [Fact]
    public void Should_Have_Search_Fields_Enum_Values()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        // Check that search_fields has items with enum values
        argumentProperties["search_fields"].ShouldContainKey("items");
        var items = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentProperties["search_fields"]["items"].ToString()!)!;
        items["type"].ToString().ShouldBe("string");
        items.ShouldContainKey("enum");

        var enumValues = JsonSerializer.Deserialize<string[]>(items["enum"].ToString()!)!;
        enumValues.ShouldContain("title");
        enumValues.ShouldContain("description");
        enumValues.ShouldContain("summary");
        enumValues.ShouldContain("resolutionDescription");
    }

    [Fact]
    public void Should_Have_Default_Limit_Value()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        // Check that limit has default value
        argumentProperties["limit"].ShouldContainKey("default");
        argumentProperties["limit"]["default"].ToString().ShouldBe("20");
    }

    [Fact]
    public void Should_Reject_Missing_Required_Keywords()
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
    public void Should_Reject_Null_Keywords()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement((string?)null)
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
    public void Should_Reject_Empty_Keywords()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Keywords cannot be empty");
    }

    [Fact]
    public void Should_Reject_Whitespace_Only_Keywords()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("   ")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Keywords cannot be empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Should_Reject_Invalid_Limit_Values(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("test keyword"),
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
        else if (limit > 500)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            ((TextContentBlock)result.Content[0]).Text.ShouldContain("Limit cannot exceed 500");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(500)]
    public void Should_Not_Reject_Valid_Limit_Values_Due_To_Validation(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("test keyword"),
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
            errorText.ShouldNotContain("Limit cannot exceed 500");
        }
    }

    [Fact]
    public void Should_Accept_Valid_Keywords_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("test keyword")
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
            errorText.ShouldNotContain("Keywords cannot be empty");
        }
    }

    [Fact]
    public void Should_Accept_Valid_Search_Fields()
    {
        // Arrange
        var searchFields = new[] { "title", "description" };
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("test keyword"),
            ["search_fields"] = JsonSerializer.SerializeToElement(searchFields)
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
            errorText.ShouldNotContain("Invalid search field");
        }
    }

    [Fact]
    public void Should_Handle_All_Arguments_Without_Validation_Error()
    {
        // Arrange
        var searchFields = new[] { "title", "summary", "description" };
        var arguments = new Dictionary<string, JsonElement>
        {
            ["keywords"] = JsonSerializer.SerializeToElement("test keyword"),
            ["search_fields"] = JsonSerializer.SerializeToElement(searchFields),
            ["limit"] = JsonSerializer.SerializeToElement(50)
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
            errorText.ShouldNotContain("Keywords cannot be empty");
            errorText.ShouldNotContain("Limit must be greater than 0");
            errorText.ShouldNotContain("Limit cannot exceed 500");
        }
    }

    [Fact]
    public void Should_Have_Correct_McpTool_Structure()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;

        // Assert
        mcpTool.Name.ShouldBe("search_issues_by_keywords");
        mcpTool.Description.ShouldBe("Search for tickets using SQLite FTS5 testing for keywords in multiple fields");
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
        properties.Count.ShouldBe(3); // keywords, search_fields, and limit
    }
}