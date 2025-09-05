using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteTable(tableName: "OperationParameters")]
[JfSQLiteIndex(nameof(PackageKey), nameof(OperationKey), nameof(OperationParameterOrder))]
[JfSQLiteIndex(nameof(OperationKey), nameof(OperationParameterOrder))]
public partial class CgDbOperationParameter : CgDbPackageContentBase
{
    [JfSQLiteForeignKey(referenceTable: "Operations", referenceColumn: nameof(CgDbOperation.Key))]
    public required int OperationKey { get; set; }

    public required string Name { get; set; }
    public required string Use { get; set; }
    public required string? Scopes { get; set; }
    [JfSQLiteIgnore]
    public List<string> ScopeList
    {
        set
        {
            Scopes = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(Scopes))
            {
                return [];
            }
            return Scopes
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required int Min { get; set; }
    public required string Max { get; set; }
    public required string? Documentation { get; set; }
    public required string? Type { get; set; }
    public required string? AllowedTypes { get; set; }
    [JfSQLiteIgnore]
    public List<string> AllowedTypeList
    {
        set
        {
            AllowedTypes = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(AllowedTypes))
            {
                return [];
            }
            return AllowedTypes
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string? TargetProfileCanonicals { get; set; }
    [JfSQLiteIgnore]
    public List<string> TargetProfileCanonicalList
    {
        set
        {
            TargetProfileCanonicals = (value == null || value.Count == 0) ? null : string.Join(',', value);
        }
        get
        {
            if (string.IsNullOrEmpty(TargetProfileCanonicals))
            {
                return [];
            }
            return TargetProfileCanonicals
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }

    public required string? SearchType { get; set; }

    public required string? BindingStrength { get; set; }
    public required string? BindingValueSetCanonical { get; set; }

    public required List<object>? ReferencedFrom { get; set; }


    [JfSQLiteForeignKey(referenceTable: "OperationParameters", referenceColumn: nameof(CgDbOperationParameter.Key))]
    public required int? ParentParameterKey { get; set; }

    public required int ChildParameterCount { get; set; }

    public required int OperationParameterOrder { get; set; }
    public required int ParameterPartOrder { get; set; }
}
