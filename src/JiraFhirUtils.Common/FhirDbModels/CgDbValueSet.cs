using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;


[JfSQLiteTable(tableName: "ValueSets")]
[JfSQLiteIndex(nameof(PackageKey), nameof(StrongestBindingCore))]
[JfSQLiteIndex(nameof(PackageKey), nameof(UnversionedUrl))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Name))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Id))]
public partial class CgDbValueSet : CgDbMetadataResourceBase
{
    public required bool CanExpand { get; set; }
    public required bool? HasEscapeValveCode { get; set; }
    public required string? Message { get; set; }
    public required bool IsExcluded { get; set; } = false;

    public required int ConceptCount { get; set; }
    public required int ActiveConcreteConceptCount { get; set; }
    public required string? ReferencedSystems { get; set; }

    public required int BindingCountCore { get; set; }
    public required string? StrongestBindingCore { get; set; }
    public required string? StrongestBindingCoreCode { get; set; }
    public required string? StrongestBindingCoreCoding { get; set; }

    public required int BindingCountExtended { get; set; }
    public required string? StrongestBindingExtended { get; set; }
    public required string? StrongestBindingExtendedCode { get; set; }
    public required string? StrongestBindingExtendedCoding { get; set; }

    public required object? Compose { get; set; }

    [JfSQLiteIgnore]
    public string UiDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Name))
            {
                return "-";
            }

            return $"{Name}: {VersionedUrl}";
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

            return $"{Name}: {VersionedUrl}, Concepts: {ConceptCount}" +
                (string.IsNullOrEmpty(Description) ? string.Empty : " - " + Description);
        }
    }
}
