namespace JiraFhirUtils.Common;

/// <summary>
/// A JIRA Issue record, as represented in the database.
/// </summary>
public record class IssueRecord
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string? Title { get; init; }
    public required string? IssueUrl { get; init; }
    public required string? ProjectId { get; init; }
    public required string? ProjectKey { get; init; }
    public required string? Description { get; init; }
    public required string? Summary { get; init; }
    public required string? Type { get; init; }
    public required string? TypeId { get; init; }
    public required string? Priority { get; init; }
    public required string? PriorityId { get; init; }
    public required string? Status { get; init; }
    public required string? StatusId { get; init; }
    public required string? StatusCategoryId { get; init; }
    public required string? StatusCategoryKey { get; init; }
    public required string? StatusCategoryColor { get; init; }
    public required string? Resolution { get; init; }
    public required string? ResolutionId { get; init; }
    public required string? Assignee { get; init; }
    public required string? Reporter { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required DateTime? UpdatedAt { get; init; }
    public required DateTime? ResolvedAt { get; init; }
    public required string? Watches { get; init; }
    public required string? Specification { get; init; }
    public required string? AppliedForVersion { get; init; }
    public required string? ChangeCategory { get; init; }
    public required string? ChangeImpact { get; init; }
    public required string? DuplicateIssue { get; init; }
    public required string? Grouping { get; init; }
    public required string? RaisedInVersion { get; init; }
    public required string? RelatedIssues { get; init; }
    public required string? RelatedArtifacts { get; init; }
    public required string? RelatedPages { get; init; }
    public required string? RelatedSections { get; init; }
    public required string? RelatedURL { get; init; }
    public required string? ResolutionDescription { get; init; }
    public required DateTime? VoteDate { get; init; }
    public required string? Vote { get; init; }
    public required string? WorkGroup { get; init; }
}

/// <summary>
/// A JIRA Comment record, as represented in the database.
/// </summary>
public record class CommentRecord
{
    public required int Id { get; init; }
    public required int CommentId { get; init; }
    public required int IssueId { get; init; }
    public required string IssueKey { get; init; }
    public required string Author { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string Body { get; init; }
}

/// <summary>
/// A JIRA Custom Field record, as represented in the database.
/// </summary>
public record class CustomFieldRecord
{
    public int Id { get; init; }
    public required string IssueKey { get; init; }
    public required string? FieldId { get; init; }
    public required string? FieldKey { get; init; }
    public required string? FieldName { get; init; }
    public required string? FieldValue { get; init; }
}
