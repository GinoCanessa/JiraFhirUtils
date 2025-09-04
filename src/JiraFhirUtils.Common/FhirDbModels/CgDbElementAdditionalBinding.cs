using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "ElementAdditionalBindings")]
[JfSQLiteIndex(nameof(ElementKey))]
public partial class CgDbElementAdditionalBinding : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public int StructureKey { get; set; }

    [JfSQLiteForeignKey(referenceTable: "Elements", referenceColumn: nameof(CgDbElement.Key))]
    public required int ElementKey { get; set; }

    public required string? FhirKey { get; set; }
    public required string? Purpose { get; set; }
    public required string? BindingValueSet { get; set; }
    public required int? BindingValueSetKey { get; set; }
    public required string? Documentation { get; set; }
    public required string? ShortDocumentation { get; set; }
    public required string? CollatedUsageContexts { get; set; }
    public required bool? SatisfiedBySingleRepetition { get; set; }
}
