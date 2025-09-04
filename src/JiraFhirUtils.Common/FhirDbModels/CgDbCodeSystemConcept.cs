using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "CodeSystemConcepts")]
[JfSQLiteIndex(nameof(CodeSystemKey))]
[JfSQLiteIndex(nameof(CodeSystemKey), nameof(FlatOrder))]
public partial class CgDbCodeSystemConcept : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "CodeSystems", referenceColumn: nameof(CgDbCodeSystem.Key))]
    public required int CodeSystemKey { get; set; }

    public required int FlatOrder { get; set; }
    public required int RelativeOrder { get; set; }
    public required string Code { get; set; }
    public required string? Display { get; set; }
    public required string? Definition { get; set; }
    public required List<string> Designations { get; set; }
    public required List<string> Properties { get; set; }
    public required int? ParentConceptKey { get; set; }
    public required int ChildConceptCount { get; set; }
}
