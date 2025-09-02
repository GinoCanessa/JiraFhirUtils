using System.Globalization;
using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;

namespace jf_loader.Load;

/// <summary>
/// Extension methods to map XML classes to Database record classes
/// </summary>
public static class XmlToDatabaseExtensions
{
    /// <summary>
    /// Extension method to map JiraItem to IssueRecord
    /// </summary>
    /// <param name="item">The JIRA item to convert</param>
    /// <param name="issueKey">The issue key</param>
    /// <returns>An IssueRecord with mapped properties</returns>
    public static IssueRecord ToIssueRecord(this JiraItem item, string issueKey)
    {
        return new IssueRecord
        {
            Id = item.Key.Id,
            Key = issueKey,
            Title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : null,
            IssueUrl = !string.IsNullOrWhiteSpace(item.Link) ? item.Link : null,
            ProjectId = item.Project.Id != 0 ? item.Project.Id.ToString() : null,
            ProjectKey = !string.IsNullOrWhiteSpace(item.Project.Key) ? item.Project.Key : null,
            Description = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : null,
            Summary = !string.IsNullOrWhiteSpace(item.Summary) ? item.Summary : null,
            Type = !string.IsNullOrWhiteSpace(item.Type.Name) ? item.Type.Name : null,
            TypeId = item.Type.Id != 0 ? item.Type.Id.ToString() : null,
            Priority = !string.IsNullOrWhiteSpace(item.Priority.Name) ? item.Priority.Name : null,
            PriorityId = item.Priority.Id != 0 ? item.Priority.Id.ToString() : null,
            Status = !string.IsNullOrWhiteSpace(item.Status.Name) ? item.Status.Name : null,
            StatusId = item.Status.Id != 0 ? item.Status.Id.ToString() : null,
            StatusCategoryId = item.Status.StatusCategory?.Id != 0 ? item.Status.StatusCategory?.Id.ToString() : null,
            StatusCategoryKey = !string.IsNullOrWhiteSpace(item.Status.StatusCategory?.Key) ? item.Status.StatusCategory.Key : null,
            StatusCategoryColor = !string.IsNullOrWhiteSpace(item.Status.StatusCategory?.ColorName) ? item.Status.StatusCategory.ColorName : null,
            Resolution = !string.IsNullOrWhiteSpace(item.Resolution?.Name) ? item.Resolution.Name : null,
            ResolutionId = item.Resolution?.Id != 0 ? item.Resolution?.Id.ToString() : null,
            Assignee = !string.IsNullOrWhiteSpace(item.Assignee?.Username) ? item.Assignee.Username : null,
            Reporter = !string.IsNullOrWhiteSpace(item.Reporter?.Username) ? item.Reporter.Username : null,
            CreatedAt = TryParseDate(item.Created),
            UpdatedAt = TryParseDate(item.Updated),
            ResolvedAt = TryParseDate(item.Resolved),
            Watches = item.Watches != 0 ? item.Watches.ToString() : null,
            
            // Initialize custom fields to null - they will be populated later by the migration process
            Specification = null,
            AppliedForVersion = null,
            ChangeCategory = null,
            ChangeImpact = null,
            DuplicateIssue = null,
            Grouping = null,
            RaisedInVersion = null,
            RelatedIssues = null,
            RelatedArtifacts = null,
            RelatedPages = null,
            RelatedSections = null,
            RelatedURL = null,
            ResolutionDescription = null,
            VoteDate = null,
            Vote = null,
            WorkGroup = null
        };
    }

    /// <summary>
    /// Extension method to map JiraComment to CommentRecord
    /// </summary>
    /// <param name="comment">The JIRA comment to convert</param>
    /// <param name="issueKey">The issue key this comment belongs to</param>
    /// <returns>A CommentRecord with mapped properties</returns>
    public static CommentRecord ToCommentRecord(this JiraComment comment, string issueKey)
    {
        return new CommentRecord
        {
            Id = 0, // Auto-increment field, will be set by database
            JiraCommentId = comment.Id,
            IssueId = 0, // This would need to be resolved from issue key to issue ID, but current schema uses issue_key
            IssueKey = issueKey,
            Author = !string.IsNullOrWhiteSpace(comment.Author) ? comment.Author : string.Empty,
            CreatedAt = TryParseDate(comment.Created) ?? DateTime.UtcNow, // Use current time as fallback
            Body = !string.IsNullOrWhiteSpace(comment.Body) ? comment.Body : string.Empty
        };
    }

    /// <summary>
    /// Extension method to map JiraXmlCustomField to CustomFieldRecord
    /// </summary>
    /// <param name="customField">The JIRA custom field to convert</param>
    /// <param name="issueKey">The issue key this custom field belongs to</param>
    /// <returns>A CustomFieldRecord with mapped properties</returns>
    public static CustomFieldRecord ToCustomFieldRecord(this JiraXmlCustomField customField, string issueKey)
    {
        // Process custom field values - handle both single and multiple values
        List<JiraCustomFieldValue> customFieldValues = customField.CustomFieldValues?.Values ?? new List<JiraCustomFieldValue>();
        string? fieldValue = null;

        if (customFieldValues.Count > 1)
        {
            // Handle array of values - concatenate with comma separation
            fieldValue = string.Join(", ", customFieldValues.Select(v => v.Value));
        }
        else if (customFieldValues.Count == 1)
        {
            // Single value
            fieldValue = customFieldValues.First().Value;
        }

        return new CustomFieldRecord
        {
            Id = 0, // Auto-increment field, will be set by database
            IssueId = 0, // Will be resolved from issue key to issue ID in processing code
            IssueKey = issueKey,
            FieldId = !string.IsNullOrWhiteSpace(customField.Id) ? customField.Id : null,
            FieldKey = !string.IsNullOrWhiteSpace(customField.Key) ? customField.Key : null,
            FieldName = !string.IsNullOrWhiteSpace(customField.CustomFieldName) ? customField.CustomFieldName : null,
            FieldValue = !string.IsNullOrWhiteSpace(fieldValue) ? fieldValue : null
        };
    }

    /// <summary>
    /// Helper method to parse date strings to DateTime
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <returns>Parsed DateTime or null if invalid</returns>
    private static DateTime? TryParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime date))
        {
            return date;
        }
        
        return null;
    }
}

/// <summary>
/// Extension methods for centralized SQLite parameter handling
/// </summary>
public static class SqliteParameterExtensions
{
    /// <summary>
    /// Adds a parameter with proper null handling, converting null values to DBNull.Value
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="command">The SQLite command</param>
    /// <param name="parameterName">The parameter name (should include $ prefix)</param>
    /// <param name="value">The value to set</param>
    public static void AddParameterWithValue<T>(this SqliteCommand command, string parameterName, T? value)
    {
        command.Parameters.AddWithValue(parameterName, (object?)value ?? DBNull.Value);
    }
    
    /// <summary>
    /// Sets a parameter value with proper null handling, converting null values to DBNull.Value
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="command">The SQLite command</param>
    /// <param name="parameterName">The parameter name (should include $ prefix)</param>
    /// <param name="value">The value to set</param>
    public static void SetParameterValue<T>(this SqliteCommand command, string parameterName, T? value)
    {
        command.Parameters[parameterName].Value = (object?)value ?? DBNull.Value;
    }
    
    /// <summary>
    /// Formats a DateTime for database storage, returning DBNull.Value for null dates
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>Formatted date string or DBNull.Value</returns>
    public static object FormatDateTimeForDatabase(DateTime? dateTime)
    {
        return dateTime?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) ?? (object)DBNull.Value;
    }
}