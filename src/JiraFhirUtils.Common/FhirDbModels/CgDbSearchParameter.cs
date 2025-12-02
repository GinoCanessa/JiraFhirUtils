using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "SearchParameters")]
[JfSQLiteIndex(nameof(PackageKey), nameof(UnversionedUrl))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Name))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Id))]
public partial class CgDbSearchParameter : CgDbMetadataResourceBase
{
    public required string? DerivedFromCanonical { get; set; }

    public required string Code { get; set; }

    public required string? AliasCodes { get; set; }
    [JfSQLiteIgnore]
    public List<string> AliasCodeList
    {
        set
        {
            AliasCodes = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(AliasCodes))
            {
                return [];
            }
            return AliasCodes.Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string BaseResources { get; set; }
    [JfSQLiteIgnore]
    public List<string> BaseResourceList
    {
        set
        {
            BaseResources = (value == null || value.Count == 0) ? string.Empty : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(BaseResources))
            {
                return [];
            }
            return BaseResources
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }
    public required string? AdditionalBaseResources { get; set; }
    [JfSQLiteIgnore]
    public List<string> AdditionalBaseResourceList
    {
        set
        {
            AdditionalBaseResources = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(AdditionalBaseResources))
            {
                return [];
            }
            return AdditionalBaseResources
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string? SearchType { get; set; }
    public required string? Expression { get; set; }
    public required string? ProcessingMode { get; set; }
    public required string? SearchParameterConstraint { get; set; }
    public required string? ReferenceTargets { get; set; }
    [JfSQLiteIgnore]
    public List<string> ReferenceTargetList
    {
        set
        {
            ReferenceTargets = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(ReferenceTargets))
            {
                return [];
            }
            return ReferenceTargets
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required bool? MultipleOr { get; set; }
    public required bool? MultipleAnd { get; set; }
    public required string? Comparators { get; set; }
    [JfSQLiteIgnore]
    public List<string> ComparatorList
    {
        set
        {
            Comparators = (value == null || value.Count == 0)
                ? null
                : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(Comparators))
            {
                return [];
            }
            return Comparators
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string? Modifiers { get; set; }
    [JfSQLiteIgnore]
    public List<string> ModifierList
    {
        set
        {
            Modifiers = (value == null || value.Count == 0)
                ? null
                : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(Modifiers))
            {
                return [];
            }
            return Modifiers
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string? ChainableSearchParameters { get; set; }
    [JfSQLiteIgnore]
    public List<string> ChainableSearchParameterList
    {
        set
        {
            ChainableSearchParameters = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(ChainableSearchParameters))
            {
                return [];
            }
            return ChainableSearchParameters
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required int ComponentCount { get; set; }
}
