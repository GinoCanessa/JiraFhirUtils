using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "Structures")]
[JfSQLiteIndex(nameof(PackageKey), nameof(ArtifactClass))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Name))]
[JfSQLiteIndex(nameof(PackageKey), nameof(UnversionedUrl))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Id))]
public partial class CgDbStructure : CgDbMetadataResourceBase
{
    public required string? Comment { get; set; }
    public required string? Message { get; set; }

    public required string ArtifactClass { get; set; } = "Unknown";

    public required int SnapshotCount { get; set; }
    public required int DifferentialCount { get; set; }

    public required string? Implements { get; set; }

    [JfSQLiteIgnore]
    public string UiDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Name))
            {
                return "-";
            }

            return $"{Name}" +
                (string.IsNullOrEmpty(Title) ? string.Empty : " - " + Title);
        }
    }

    [JfSQLiteIgnore]
    public string UiDisplayLong
    {
        get
        {
            if (string.IsNullOrEmpty(Name))
            {
                return "-";
            }

            return $"{Name}, Snapshot: {SnapshotCount}, Diff: {DifferentialCount}" +
                (string.IsNullOrEmpty(Title) ? string.Empty : " - " + Title) +
                (string.IsNullOrEmpty(Description) ? string.Empty : " - " + Description);
        }
    }
}
