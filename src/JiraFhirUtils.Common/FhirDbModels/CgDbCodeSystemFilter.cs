using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "CodeSystemFilters")]
[JfSQLiteIndex(nameof(CodeSystemKey))]
public partial class CgDbCodeSystemFilter : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "CodeSystems", referenceColumn: nameof(CgDbCodeSystem.Key))]
    public required int CodeSystemKey { get; set; }

    public required string Code { get; set; }
    public required string? Description { get; set; }
    public required string Operators { get; set; }
    public required string Value { get; set; }
}
