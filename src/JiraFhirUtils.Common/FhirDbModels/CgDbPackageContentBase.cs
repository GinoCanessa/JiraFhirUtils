using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteBaseClass]
public abstract class CgDbPackageContentBase : CgDbBase
{
    [JfSQLiteForeignKey(referenceTable: "Packages", referenceColumn: nameof(CgDbPackage.Key))]
    public required int PackageKey { get; set; }
}
