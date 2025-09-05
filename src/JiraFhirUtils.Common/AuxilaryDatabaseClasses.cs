using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;



[JfSQLiteTable("lemmas")]
public partial record class LemmaRecord
{
    public required string Inflection { get; set; }
    public required string Category { get; set; }
    public required string Lemma { get; set; }
}