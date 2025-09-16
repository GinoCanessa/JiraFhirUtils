using JiraFhirUtils.SQLiteGenerator;
using System.Data;

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
[JfSQLiteIndex(nameof(IssueId), nameof(Count))]
[JfSQLiteIndex(nameof(IssueId), nameof(KeywordType), nameof(Count))]
[JfSQLiteIndex(nameof(Keyword))]
public partial record class DbIssueKeywordRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    [JfSQLiteForeignKey(referenceTable: "issues", referenceColumn: "Id")]
    public required int IssueId { get; set; }

    public required string Keyword { get; set; }
    public required int Count { get; set; }
    public required KeywordTypeCodes KeywordType { get; set; }
    public double? Bm25Score { get; set; }

    public static bool ValidateIssueKeywordsExist(IDbConnection db)
    {
        using IDbCommand command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM issue_keywords";
        object? result = command.ExecuteScalar();
        return result != null && Convert.ToInt32(result) > 0;
    }
}

[JfSQLiteTable("corpus_keywords")]
[JfSQLiteIndex(nameof(Keyword), nameof(KeywordType))]
[JfSQLiteIndex(nameof(Count))]
[JfSQLiteIndex(nameof(KeywordType), nameof(Count))]
public partial record class DbCorpusKeywordRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }
    public required string Keyword { get; set; }
    public required int Count { get; set; }
    public required KeywordTypeCodes KeywordType { get; set; }
    public double? Idf { get; set; }

    public static bool ValidateCorpusKeywordsExist(IDbConnection db)
    {
        return DbCorpusKeywordRecord.SelectCount(db) > 0;
    }
}

[JfSQLiteTable("total_frequencies")]

public partial record class DbTotalFrequencyRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    [JfSQLiteForeignKey(referenceTable: "issues", referenceColumn: "Id")]
    public required int? IssueId { get; set; }

    public int TotalWords { get; set; } = 0;
    public int TotalLemmaWords { get; set; } = 0;
    public int TotalStopWords { get; set; } = 0;
    public int TotalFhirElementPaths { get; set; } = 0;
    public int TotalFhirOperationNames { get; set; } = 0;
}

[JfSQLiteTable("bm25_config")]
public partial record class DbBm25ConfigRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }
    
    public double K1 { get; set; } = 1.2;
    public double B { get; set; } = 0.75;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[JfSQLiteTable("document_stats")]
public partial record class DbDocumentStatsRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; }

    public double AverageDocumentLength { get; set; }
    public int TotalDocumentCount { get; set; }
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
}
