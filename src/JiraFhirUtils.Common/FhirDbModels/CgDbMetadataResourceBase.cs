using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common.FhirDbModels;

[JfSQLiteBaseClass]
public abstract class CgDbMetadataResourceBase : CgDbPackageContentBase
{
    public required string Id { get; set; }
    public required string VersionedUrl { get; set; }
    public required string UnversionedUrl { get; set; }
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string? VersionAlgorithmString { get; set; }
    public required object? VersionAlgorithmCoding { get; set; }
    public required string? Status { get; set; }
    public required string? Title { get; set; }
    public required string? Description { get; set; }
    public required string? Purpose { get; set; }
    public required object? Narrative { get; set; }
    public required string? StandardStatus { get; set; }
    public required string? WorkGroup { get; set; }
    public required int? FhirMaturity { get; set; }
    public required bool? IsExperimental { get; set; }
    public required DateTimeOffset? LastChangedDate { get; set; }
    public required string? Publisher { get; set; }
    public required string? Copyright { get; set; }
    public required string? CopyrightLabel { get; set; }
    public required string? ApprovalDate { get; set; }
    public required string? LastReviewDate { get; set; }
    public required DateTimeOffset? EffectivePeriodStart { get; set; }
    public required DateTimeOffset? EffectivePeriodEnd { get; set; }
    public required List<object>? Topic { get; set; }
    public required List<object>? RelatedArtifacts { get; set; }
    public required List<object>? Jurisdictions { get; set; }
    public required List<object>? UseContexts { get; set; }
    public required List<object>? Contacts { get; set; }
    public required List<object>? Authors { get; set; }
    public required List<object>? Editors { get; set; }
    public required List<object>? Reviewers { get; set; }
    public required List<object>? Endorsers { get; set; }
    public required List<object>? RootExtensions { get; set; }
    public required string? SourcePackageMoniker { get; set; }
}
