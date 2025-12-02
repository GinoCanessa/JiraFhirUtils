using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "ElementCollatedTypes")]
[JfSQLiteIndex(nameof(ElementKey))]
[JfSQLiteIndex(nameof(ElementKey), nameof(TypeName))]
public partial class CgDbElementCollatedType : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public required int StructureKey { get; set; }

    [JfSQLiteForeignKey(referenceTable: "Elements", referenceColumn: nameof(CgDbElement.Key))]
    public required int ElementKey { get; set; }
    public required string CollatedLiteral { get; set; }

    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public required int? TypeStructureKey { get; set; }
    public required string TypeName { get; set; }
}
