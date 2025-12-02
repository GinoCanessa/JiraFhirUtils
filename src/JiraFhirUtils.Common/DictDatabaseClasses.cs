using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;

[JfSQLiteTable("words")]
[JfSQLiteIndex(nameof(Word))]
public partial record class DictWordRecord
{
    [JfSQLiteUnique]
    public required string Word { get; set; }
}

[JfSQLiteTable("typos")]
[JfSQLiteIndex(nameof(Typo), nameof(Correction))]
public partial record class DictTypoRecord
{
    [JfSQLiteUnique]
    public required string Typo { get; set; }
    public required string Correction { get; set; }
}