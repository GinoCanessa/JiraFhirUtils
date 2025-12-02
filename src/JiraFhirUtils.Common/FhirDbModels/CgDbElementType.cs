using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "ElementTypes")]
[JfSQLiteIndex(nameof(ElementKey))]
[JfSQLiteIndex(nameof(ElementKey), nameof(TypeName))]
[JfSQLiteIndex(nameof(ElementKey), nameof(TypeName), nameof(TypeProfile), nameof(TargetProfile))]
[JfSQLiteIndex(nameof(TypeName))]
[JfSQLiteIndex(nameof(TypeName), nameof(TypeProfile), nameof(TargetProfile))]
public partial class CgDbElementType : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public required int StructureKey { get; set; }

    [JfSQLiteForeignKey(referenceTable: "Elements", referenceColumn: nameof(CgDbElement.Key))]
    public required int ElementKey { get; set; }

    [JfSQLiteForeignKey(referenceTable: "CollatedTypes", referenceColumn: nameof(CgDbElementCollatedType.Key))]
    public required int CollatedTypeKey { get; set; }

    public required string? TypeName { get; set; }
    public required string? TypeProfile { get; set; }
    public required string? TargetProfile { get; set; }

    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public required int? TypeStructureKey { get; set; }

    [JfSQLiteIgnore]
    public string Literal =>
        (string.IsNullOrEmpty(TypeName) ? string.Empty : TypeName) +
        (string.IsNullOrEmpty(TypeProfile) ? string.Empty : $"[{TypeProfile}]") +
        (string.IsNullOrEmpty(TargetProfile) ? string.Empty : $"({TargetProfile})");

}
