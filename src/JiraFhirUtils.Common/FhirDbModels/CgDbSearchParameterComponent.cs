using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "SearchParameterComponents")]
[JfSQLiteIndex(nameof(PackageKey), nameof(DefinitionCanonical))]
[JfSQLiteIndex(nameof(PackageKey), nameof(SearchParameterKey))]
[JfSQLiteIndex(nameof(SearchParameterKey))]
public partial class CgDbSearchParameterComponent : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "SearchParameters", referenceColumn: nameof(CgDbSearchParameter.Key))]
    public required int SearchParameterKey { get; set; }

    public required string DefinitionCanonical { get; set; }

    public required string Expression { get; set; }
}
