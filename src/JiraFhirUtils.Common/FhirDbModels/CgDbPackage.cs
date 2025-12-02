using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;



[JfSQLiteTable(tableName: "Packages")]
public partial class CgDbPackage : CgDbBase
{
    public required string Name { get; set; }
    public required string PackageId { get; set; }
    public required string PackageVersion { get; set; }
    public required string FhirVersionShort { get; set; }
    public required string CanonicalUrl { get; set; }
    public required string? WebUrl { get; set; }
    public required string ShortName { get; set; }
    public required string? Title { get; set; }
    public required string? Description { get; set; }

    public required string? Dependencies { get; set; }

    public required string DefinitionFhirSequence { get; set; } = "Unknown";

    [JfSQLiteIgnore]
    public string NpmId => (string.IsNullOrEmpty(PackageId) && string.IsNullOrEmpty(PackageVersion))
        ? string.Empty
        : $"{PackageId}@{PackageVersion}";

    [JfSQLiteIgnore]
    public string CacheFolderName => (string.IsNullOrEmpty(PackageId) && string.IsNullOrEmpty(PackageVersion))
        ? string.Empty
        : $"{PackageId}#{PackageVersion}";
}

