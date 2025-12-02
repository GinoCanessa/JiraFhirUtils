using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "Operations")]
[JfSQLiteIndex(nameof(PackageKey), nameof(UnversionedUrl))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Name))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Code))]
[JfSQLiteIndex(nameof(PackageKey), nameof(Id))]
public partial class CgDbOperation : CgDbMetadataResourceBase
{
    public required string Kind { get; set; }
    public required bool? AffectsState { get; set; }
    public required string? Synchronicity { get; set; }         // TODO: ValueSet is in the Extensions pack...
    public required string? Code { get; set; }
    public required string? Comment { get; set; }
    public required string? BaseCanonical { get; set; }
    public required string? ResourceTypes { get; set; }
    [JfSQLiteIgnore]
    public List<string> ResourceTypeList
    {
        set
        {
            ResourceTypes = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(ResourceTypes))
            {
                return [];
            }
            return ResourceTypes
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    
    public required string? AdditionalResourceTypes { get; set; }
    [JfSQLiteIgnore]
    public List<string> AdditionalResourceTypeList
    {
        set
        {
            AdditionalResourceTypes = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(AdditionalResourceTypes))
            {
                return [];
            }
            return AdditionalResourceTypes
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }
    
    public required bool InvokeOnSystem { get; set; }
    public required bool InvokeOnType { get; set; }
    public required bool InvokeOnInstance { get; set; }

    public required string? InputProfileCanonical { get; set; }
    public required string? OutputProfileCanonical { get; set; }

    public required int ParameterCount { get; set; }

    public required List<object>? Overloads { get; set; }
}
