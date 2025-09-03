using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;

public enum JiraPriorityCodes : int
{
    Highest = 1,
    VeryHigh = 6,
    High = 2,
    MediumHigh = 10000,
    Medium = 3,
    Low = 4,
    VeryLow = 7,
    Lowest = 5,
}

public enum  JiraStatusCodes : int
{
    Applied = 10107,
    Deferred = 10306,
    Duplicate = 10106,
    Published = 10108,
    ResolvedNoChange = 10104,
    ResolvedChangeRequired = 10105,
    Submitted = 10101,
    Triaged = 10102,
    WaitingForInput = 10103,
}

public enum JiraIssueTypeCodes : int
{
    ChangeRequest = 10600,
    Comment = 10802,
    Question = 10801,
    TechnicalCorrection = 10800,
}

public enum JiraResolutionCodes : int
{
    ConsideredNoActionRequired = 10200,
    ConsideredQuestionAnswered = 10201,
    ConsideredForFutureUse = 10202,
    Duplicate = 10002,
    NotPersuasive = 10207,
    NotPersuasiveWithModification = 10203,
    Persuasive = 10205,
    PersuasiveWithModification = 10206,
    Retracted = 10400,
    Unresolved = -1,
}

/// <summary>
/// A JIRA Issue record, as represented in the database.
/// </summary>

[JfSQLiteTable("issues")]
[JfSQLiteIndex(nameof(Key))]
[JfSQLiteIndex(nameof(ProjectKey), nameof(Key))]
public partial record class IssueRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    [JfSQLiteUnique]
    public required string Key { get; set; }
    public required string Title { get; set; }
    public required string IssueUrl { get; set; }
    public required int ProjectId { get; set; }
    public required string ProjectKey { get; set; }
    public required string Description { get; set; }
    public required string? Summary { get; set; }
    public required string Type { get; set; }
    public required int TypeId { get; set; }
    public required string? Priority { get; set; }
    public required int? PriorityId { get; set; }
    public required string? Status { get; set; }
    public required int StatusId { get; set; }
    public required string Resolution { get; set; }
    public required int ResolutionId { get; set; }
    public required string? Assignee { get; set; }
    public required string? Reporter { get; set; }
    public required DateTime? CreatedAt { get; set; }
    public required DateTime? UpdatedAt { get; set; }
    public required DateTime? ResolvedAt { get; set; }
    public required string? Watches { get; set; }
    public required string? Specification { get; set; }
    public required string? AppliedForVersion { get; set; }
    public required string? ChangeCategory { get; set; }
    public required string? ChangeImpact { get; set; }
    public required string? DuplicateIssue { get; set; }
    public required string? DuplicateVotedIssue { get; set; }
    public required string? Grouping { get; set; }
    public required string? RaisedInVersion { get; set; }
    public required string? RelatedIssues { get; set; }
    public required string? RelatedArtifacts { get; set; }
    public required string? RelatedPages { get; set; }
    public required string? RelatedSections { get; set; }
    public required string? RelatedUrl { get; set; }
    public required string? ResolutionDescription { get; set; }
    public required DateTime? VoteDate { get; set; }
    public required string? Vote { get; set; }
    public required string? BlockVote { get; set; }
    public required string? WorkGroup { get; set; }
    public required string? SelectedBallot { get; set; }
    public required string? RequestInPerson { get; set; }
}

/// <summary>
/// A JIRA Comment record, as represented in the database.
/// </summary>
[JfSQLiteTable("comments")]
[JfSQLiteIndex(nameof(IssueKey))]
public partial record class CommentRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    [JfSQLiteForeignKey(referenceColumn: nameof(IssueRecord.Id))]
    public required int IssueId { get; set; }
    public required string IssueKey { get; set; }

    public required string Author { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string Body { get; set; }
}

/// <summary>
/// A JIRA Custom Field record, as represented in the database.
/// </summary>
[JfSQLiteTable("custom_fields")]
[JfSQLiteIndex(nameof(IssueId))]
[JfSQLiteIndex(nameof(IssueKey))]
public partial record class CustomFieldRecord
{
    [JfSQLiteKey]
    public int Id { get; set; }

    [JfSQLiteForeignKey(referenceColumn:nameof(IssueRecord.Id))]
    public required int IssueId { get; set; }
    public required string IssueKey { get; set; }

    public required string? FieldId { get; set; }
    public required string? FieldKey { get; set; }
    public required string? FieldName { get; set; }
    public required string? FieldValue { get; set; }
}
