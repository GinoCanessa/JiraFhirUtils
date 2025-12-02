using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "CodeSystemConceptProperties")]
[JfSQLiteIndex(nameof(CodeSystemPropertyDefinitionKey))]
public partial class CgDbCodeSystemConceptProperty : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "CodeSystemConcepts", referenceColumn: nameof(CgDbCodeSystemConcept.Key))]
    public required int CodeSystemConceptKey { get; set; }

    [JfSQLiteForeignKey(referenceTable: "CodeSystemPropertyDefinitions", referenceColumn: nameof(CgDbCodeSystemPropertyDefinition.Key))]
    public required int CodeSystemPropertyDefinitionKey { get; set; }

    public required string Code { get; set; }
    public required string Type { get; set; }
    public required string Value { get; set; }
}
