using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "Elements")]
[JfSQLiteIndex(nameof(StructureKey))]
[JfSQLiteIndex(nameof(StructureKey), nameof(Id))]
[JfSQLiteIndex(nameof(StructureKey), nameof(Path))]
[JfSQLiteIndex(nameof(StructureKey), nameof(ResourceFieldOrder))]
[JfSQLiteIndex(nameof(ParentElementKey), nameof(ResourceFieldOrder))]
[JfSQLiteIndex(nameof(BindingValueSetKey))]
public partial class CgDbElement : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public int StructureKey { get; set; }

    public required int? ParentElementKey { get; set; }

    public required int ResourceFieldOrder { get; set; }
    public required int ComponentFieldOrder { get; set; }
    public required string Id { get; set; }
    public required string Path { get; set; }
    public required int ChildElementCount { get; set; }
    public required string Name { get; set; }
    public required string? Short { get; set; }
    public required string? Definition { get; set; }
    public required int MinCardinality { get; set; }
    public required int MaxCardinality { get; set; }
    public required string MaxCardinalityString { get; set; }

    [JfSQLiteIgnore]
    public string FhirCardinalityString => $"{MinCardinality}..{MaxCardinalityString}";

    public required string? SliceName { get; set; }

    public required string FullCollatedTypeLiteral { get; set; }

    public required string? ValueSetBindingStrength { get; init; }
    public required string? BindingValueSet { get; set; }
    public required int? BindingValueSetKey { get; set; }
    public required int AdditionalBindingCount { get; set; }
    public required string? BindingDescription { get; set; }

    public required bool IsInherited { get; set; }
    public required string? BasePath { get; set; }
    [JfSQLiteForeignKey(referenceTable: "Elements", referenceColumn: nameof(CgDbElement.Key))]
    public required int? BaseElementKey { get; set; }
    [JfSQLiteForeignKey(referenceTable: "Structures", referenceColumn: nameof(CgDbStructure.Key))]
    public required int? BaseStructureKey { get; set; }

    public required bool IsSimpleType { get; set; }
    public required bool IsModifier { get; set; }
    public required string? IsModifierReason { get; set; }


    [JfSQLiteIgnore]
    public string UiDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(Id))
            {
                return "-";
            }

            return $"{Id}" +
                (string.IsNullOrEmpty(Short) ? string.Empty : " - " + Short);
        }
    }

    [JfSQLiteIgnore]
    public string UiDisplayWithType
    {
        get
        {
            if (string.IsNullOrEmpty(Id))
            {
                return "-";
            }

            return $"{Id} ({MinCardinality}..{MaxCardinalityString}, {FullCollatedTypeLiteral.Replace("http://hl7.org/fhir/StructureDefinition/", string.Empty)})";
        }
    }

    [JfSQLiteIgnore]
    public string UiDisplayLong
    {
        get
        {
            if (string.IsNullOrEmpty(Id))
            {
                return "-";
            }

            return $"{Id} ({MinCardinality}..{MaxCardinalityString}, {FullCollatedTypeLiteral.Replace("http://hl7.org/fhir/StructureDefinition/", string.Empty)})" +
                (string.IsNullOrEmpty(Short) ? string.Empty : " - " + Short);
        }
    }

    private static CgDbElement _empty = EmptyCopy;

    [JfSQLiteIgnore]
    public bool IsEmpty => Key == -1 && string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Path);

    [JfSQLiteIgnore]
    public static CgDbElement Empty => _empty;

    [JfSQLiteIgnore]
    public static CgDbElement EmptyCopy => new()
    {
        Key = -1,
        PackageKey = -1,
        StructureKey = -1,
        ParentElementKey = null,
        ResourceFieldOrder = -1,
        ComponentFieldOrder = -1,
        Id = string.Empty,
        Path = string.Empty,
        ChildElementCount = 0,
        Name = string.Empty,
        Short = null,
        Definition = null,
        MinCardinality = 0,
        MaxCardinality = 0,
        MaxCardinalityString = string.Empty,
        SliceName = null,
        FullCollatedTypeLiteral = string.Empty,
        ValueSetBindingStrength = null,
        BindingValueSet = null,
        BindingValueSetKey = null,
        BindingDescription = null,
        AdditionalBindingCount = 0,
        IsInherited = false,
        BasePath = null,
        BaseElementKey = null,
        BaseStructureKey = null,
        IsSimpleType = false,
        IsModifier = false,
        IsModifierReason = null,
    };
}
