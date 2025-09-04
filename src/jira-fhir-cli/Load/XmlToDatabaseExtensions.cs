using System.Globalization;
using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;

namespace jira_fhir_cli.Load;

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
        string title = item.Title;
        if (title.StartsWith('[') &&
            !string.IsNullOrEmpty(item.Project.Key) &&
            title.StartsWith("[" + item.Project.Key))
        {
            int closingBracketIndex = title.IndexOf(']');
            if (closingBracketIndex > 0)
            {
                title = title[(closingBracketIndex + 1)..].Trim();
            }
        }

        return new IssueRecord
        {
            Id = item.Key.Id,
            Key = issueKey,
            Title = title,
            IssueUrl = item.Link,
            ProjectId = item.Project.Id,
            ProjectKey = item.Project.Key,
            Description = item.Description,
            Summary = !string.IsNullOrWhiteSpace(item.Summary) ? item.Summary : null,
            Type = item.Type.Name,
            TypeId = item.Type.Id,
            Priority = !string.IsNullOrWhiteSpace(item.Priority.Name) ? item.Priority.Name : null,
            PriorityId = item.Priority.Id,
            Status = !string.IsNullOrWhiteSpace(item.Status.Name) ? item.Status.Name : null,
            StatusId = item.Status.Id,
            Resolution = !string.IsNullOrWhiteSpace(item.Resolution?.Name) ? item.Resolution.Name : JiraResolutionCodes.Unresolved.ToString(),
            ResolutionId = item.Resolution?.Id ?? (int)JiraResolutionCodes.Unresolved,
            Assignee = !string.IsNullOrWhiteSpace(item.Assignee?.Username) ? item.Assignee.Username : null,
            Reporter = !string.IsNullOrWhiteSpace(item.Reporter?.Username) ? item.Reporter.Username : null,
            CreatedAt = ParseJiraDate(item.Created),
            UpdatedAt = ParseJiraDate(item.Updated),
            ResolvedAt = ParseJiraDate(item.Resolved),
            Watches = item.Watches != 0 ? item.Watches.ToString() : null,
            
            // Initialize custom fields to null - they will be populated later by the migration process
            Specification = null,
            AppliedForVersion = null,
            ChangeCategory = null,
            ChangeImpact = null,
            DuplicateIssue = null,
            DuplicateVotedIssue = null,
            Grouping = null,
            RaisedInVersion = null,
            RelatedIssues = null,
            RelatedArtifacts = null,
            RelatedPages = null,
            RelatedSections = null,
            RelatedUrl = null,
            ResolutionDescription = null,
            VoteDate = null,
            Vote = null,
            BlockVote = null,
            WorkGroup = null,
            SelectedBallot = null,
            RequestInPerson = null,
        };
    }

    /// <summary>
    /// Extension method to map JiraComment to CommentRecord
    /// </summary>
    /// <param name="comment">The JIRA comment to convert</param>
    /// <param name="issueKey">The issue key this comment belongs to</param>
    /// <returns>A CommentRecord with mapped properties</returns>
    public static CommentRecord ToCommentRecord(this JiraComment comment, IssueRecord issueRecord)
    {
        return new CommentRecord
        {
            Id = comment.Id,
            IssueId = issueRecord.Id,
            IssueKey = issueRecord.Key,
            Author = comment.Author,
            CreatedAt = ParseJiraDate(comment.Created) ?? DateTime.UtcNow, // Use current time as fallback
            Body = comment.Body,
        };
    }

    /// <summary>
    /// Extension method to map JiraXmlCustomField to CustomFieldRecord
    /// </summary>
    /// <param name="customField">The JIRA custom field to convert</param>
    /// <param name="issueKey">The issue key this custom field belongs to</param>
    /// <returns>A CustomFieldRecord with mapped properties</returns>
    public static CustomFieldRecord ToCustomFieldRecord(this JiraXmlCustomField customField, IssueRecord issueRecord)
    {
        // Process custom field values - handle both single and multiple values
        List<JiraCustomFieldValue> customFieldValues = customField.FieldValues?.Values ?? [];

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
            Id = CustomFieldRecord.GetIndex(),
            IssueId = issueRecord.Id,
            IssueKey = issueRecord.Key,
            FieldId = !string.IsNullOrWhiteSpace(customField.FieldId) ? customField.FieldId : null,
            FieldKey = !string.IsNullOrWhiteSpace(customField.FieldKey) ? customField.FieldKey : null,
            FieldName = !string.IsNullOrWhiteSpace(customField.FieldName) ? customField.FieldName : null,
            FieldValue = !string.IsNullOrWhiteSpace(fieldValue) ? fieldValue : null
        };
    }

    /// <summary>
    /// Helper method to parse date strings to DateTime
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <returns>Parsed DateTime or null if invalid</returns>
    private static DateTime? ParseJiraDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime date))
        {
            return date;
        }
        
        return null;
    }
}
