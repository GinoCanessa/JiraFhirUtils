using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;

public enum KeywordTypeCodes : int
{
    Word = 0,
    StopWord = 1,
    FhirElementPath = 2,
    FhirOperationName = 3,
}

[JfSQLiteTable("lemmas")]
public partial record class LemmaRecord
{
    public required string Inflection { get; set; }
    public required string Category { get; set; }
    public required string Lemma { get; set; }
}

[JfSQLiteTable("issue_keywords")]
public partial record class DbIssueKeywordRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    [JfSQLiteForeignKey(referenceTable: "issues", referenceColumn: "Id")]
    public required int IssueId { get; set; }
    
    public required string Keyword { get; set; }
    public required int Count { get; set; }
    public required KeywordTypeCodes KeywordType { get; set; }
}

[JfSQLiteTable("corpus_keywords")]
public partial record class DbCorpusKeywordRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }
    public required string Keyword { get; set; }
    public required int Count { get; set; }
    public required KeywordTypeCodes KeywordType { get; set; }
}
