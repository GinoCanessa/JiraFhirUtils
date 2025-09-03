using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;

[JfSQLiteFtsTable("issues", "issues_fts")]
public partial record class IssueFtsRecord
{
    [JfSQLiteFtsUnindexed]
    public required int Id { get; set; }

    [JfSQLiteFtsUnindexed]
    public required string Key { get; set; }

    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string? Summary { get; set; }
    public required string? ResolutionDescription { get; set; }
}

[JfSQLiteFtsTable("comments", "comments_fts")]
public partial record class CommentFtsRecord
{
    [JfSQLiteFtsUnindexed]
    public required int Id { get; set; }

    [JfSQLiteFtsUnindexed]
    public required int IssueId { get; set; }

    [JfSQLiteFtsUnindexed]
    public required string IssueKey { get; set; }

    public required string Body { get; set; }
}