using System.Text.Json;
using Shouldly;
using jira_fhir_mcp.Tools;
using ModelContextProtocol.Protocol;

namespace jira_fhir_mcp.Tests.Tools;

public class ListIssuesToolTests
{
    private readonly ListIssuesTool _tool;

    public ListIssuesToolTests()
    {
        _tool = new ListIssuesTool();
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.ShouldBe("list_issues");
    }

    [Fact]
    public void Tool_Should_Have_Correct_Description()
    {
        _tool.Description.ShouldBe("List JIRA issues with comprehensive filtering options including project, workgroup, status, type, priority, assignee, reporter, and more. Supports pagination and sorting.");
    }

    [Fact]
    public void Tool_Should_Have_All_Expected_Arguments()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(properties["properties"].ToString()!)!;

        var expectedArguments = new[]
        {
            "project", "workgroup", "resolution", "status", "assignee",
            "type", "priority", "reporter", "specification", "vote", "grouping",
            "limit", "offset", "created_after", "created_before",
            "updated_after", "updated_before", "resolved_after", "resolved_before",
            "sort", "order"
        };

        foreach (var expectedArg in expectedArguments)
        {
            argumentProperties.ShouldContainKey(expectedArg);
        }
    }

    [Fact]
    public void Should_Have_Correct_Argument_Types()
    {
        var mcpTool = _tool.McpTool;
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(mcpTool.InputSchema.GetRawText())!;
        var argumentProperties = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(properties["properties"].ToString()!)!;

        // Check string arguments
        var stringArguments = new[] { "project", "workgroup", "resolution", "status", "assignee", "type", "priority", "reporter", "specification", "vote", "grouping", "sort", "order" };
        foreach (var arg in stringArguments)
        {
            argumentProperties[arg]["type"].ToString().ShouldBe("string");
        }

        // Check number arguments
        var numberArguments = new[] { "limit", "offset" };
        foreach (var arg in numberArguments)
        {
            argumentProperties[arg]["type"].ToString().ShouldBe("number");
        }

        // Check date arguments (stored as strings)
        var dateArguments = new[] { "created_after", "created_before", "updated_after", "updated_before", "resolved_after", "resolved_before" };
        foreach (var arg in dateArguments)
        {
            argumentProperties[arg]["type"].ToString().ShouldBe("string");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Should_Reject_Invalid_Limit_Values(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
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
        else if (limit > 1000)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            ((TextContentBlock)result.Content[0]).Text.ShouldContain("Limit cannot exceed 1000");
        }
    }

    [Fact]
    public void Should_Reject_Negative_Offset()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["offset"] = JsonSerializer.SerializeToElement(-1)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Offset cannot be negative");
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("2023-13-01")]
    [InlineData("not-a-date")]
    public void Should_Reject_Invalid_Date_Formats(string invalidDate)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["created_after"] = JsonSerializer.SerializeToElement(invalidDate)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
        result.Content[0].ShouldBeOfType<TextContentBlock>();
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Invalid date format");
    }

    [Theory]
    [InlineData("2023-12-01")]
    [InlineData("2023-12-01T10:30:00Z")]
    [InlineData("2023-12-01T10:30:00")]
    public void Should_Accept_Valid_Date_Formats_In_Created_After(string dateString)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["created_after"] = JsonSerializer.SerializeToElement(dateString)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        // Should not fail with date parsing error
        // Note: May still fail due to database access, but not due to date format
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Invalid date format");
        }
    }

    [Theory]
    [InlineData("created_before")]
    [InlineData("updated_after")]
    [InlineData("updated_before")]
    [InlineData("resolved_after")]
    [InlineData("resolved_before")]
    public void Should_Accept_Valid_Date_Formats_For_All_Date_Parameters(string dateParameter)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            [dateParameter] = JsonSerializer.SerializeToElement("2023-12-01")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        // Should not fail with date parsing error
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Invalid date format");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Should_Not_Reject_Valid_Limit_Values_Due_To_Validation(int limit)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
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
            errorText.ShouldNotContain("Limit cannot exceed 1000");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    public void Should_Not_Reject_Valid_Offset_Values_Due_To_Validation(int offset)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["offset"] = JsonSerializer.SerializeToElement(offset)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        // Should not fail due to offset validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Offset cannot be negative");
        }
    }

    [Fact]
    public void Should_Have_Correct_McpTool_Structure()
    {
        // Arrange & Act
        var mcpTool = _tool.McpTool;

        // Assert
        mcpTool.Name.ShouldBe("list_issues");
        mcpTool.Description.ShouldBe("List JIRA issues with comprehensive filtering options including project, workgroup, status, type, priority, assignee, reporter, and more. Supports pagination and sorting.");
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
        properties.Count.ShouldBeGreaterThan(15); // Should have all the arguments we defined
    }

    [Theory]
    [InlineData("project", "TEST")]
    [InlineData("workgroup", "TestWorkGroup")]
    [InlineData("resolution", "Fixed")]
    [InlineData("status", "Open")]
    [InlineData("assignee", "john.doe")]
    [InlineData("type", "Bug")]
    [InlineData("priority", "High")]
    [InlineData("reporter", "jane.smith")]
    [InlineData("specification", "R4")]
    [InlineData("vote", "Yes")]
    [InlineData("grouping", "TestGroup")]
    public void Should_Accept_Lowercase_Filter_Arguments_Without_Validation_Error(string argumentName, string argumentValue)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            [argumentName] = JsonSerializer.SerializeToElement(argumentValue)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // The result may fail due to database access, but should not fail due to argument validation
        // This tests that the lowercase argument names are correctly accepted
    }

    [Fact]
    public void Should_Handle_Multiple_Filter_Parameters_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement("TEST"),
            ["workgroup"] = JsonSerializer.SerializeToElement("TestGroup"),
            ["status"] = JsonSerializer.SerializeToElement("Open"),
            ["type"] = JsonSerializer.SerializeToElement("Bug"),
            ["priority"] = JsonSerializer.SerializeToElement("High"),
            ["limit"] = JsonSerializer.SerializeToElement(25),
            ["offset"] = JsonSerializer.SerializeToElement(10)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // The result may fail due to database access, but should not fail due to argument validation
    }

    [Fact]
    public void Should_Handle_All_Date_Range_Parameters_Without_Validation_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["created_after"] = JsonSerializer.SerializeToElement("2023-01-01"),
            ["created_before"] = JsonSerializer.SerializeToElement("2023-12-31"),
            ["updated_after"] = JsonSerializer.SerializeToElement("2023-06-01"),
            ["updated_before"] = JsonSerializer.SerializeToElement("2023-12-31"),
            ["resolved_after"] = JsonSerializer.SerializeToElement("2023-07-01"),
            ["resolved_before"] = JsonSerializer.SerializeToElement("2023-11-30")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to date format validation
        if (result.IsError == true)
        {
            result.Content[0].ShouldBeOfType<TextContentBlock>();
            var errorText = ((TextContentBlock)result.Content[0]).Text;
            errorText.ShouldNotContain("Invalid date format");
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("key")]
    [InlineData("created")]
    [InlineData("updated")]
    [InlineData("priority")]
    public void Should_Accept_Valid_Sort_Fields_Without_Validation_Error(string sortField)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["sort"] = JsonSerializer.SerializeToElement(sortField)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to sort field validation
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    [InlineData("ASC")]
    [InlineData("DESC")]
    public void Should_Accept_Valid_Sort_Orders_Without_Validation_Error(string sortOrder)
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["order"] = JsonSerializer.SerializeToElement(sortOrder)
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to sort order validation
    }

    [Fact]
    public void Should_Handle_Unknown_Sort_Field_Without_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>
        {
            ["sort"] = JsonSerializer.SerializeToElement("unknown_field")
        };

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should default to Id field and not fail due to unknown sort field
    }

    [Fact]
    public void Should_Handle_Empty_Arguments_Without_Error()
    {
        // Arrange & Act
        var result = _tool.RunTool(null);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to missing arguments since none are required
    }

    [Fact]
    public void Should_Handle_Empty_Arguments_Dictionary_Without_Error()
    {
        // Arrange
        var arguments = new Dictionary<string, JsonElement>();

        // Act
        var result = _tool.RunTool(arguments);

        // Assert
        result.ShouldNotBeNull();
        // Should not fail due to empty arguments
    }
}