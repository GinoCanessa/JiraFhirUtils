using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JiraFhirUtils.SQLiteGenerator;

namespace JiraFhirUtils.Common;

public enum ContentDispositionCodes
{
    Unknown = 0,
    CoreAsInformative = 1,
    CoreAsNormative = 2,
    MoveToGuide = 3,
    MoveToConfluence = 4,
    Remove = 5,
}

[JfSQLiteTable("workgroups")]
public partial record class WorkgroupRecord
{
    [JfSQLiteUnique]
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string OfficialUrl { get; set; }
    public required string? ReplacedBy { get; set; }
    public int? ConfluencePageId { get; set; } = null;
}


[JfSQLiteTable("pages")]
public partial record class SpecPageRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; } = -1;

    [JfSQLiteUnique]
    public required string PageFileName { get; set; }

    [JfSQLiteForeignKey("artifacts", nameof(ArtifactRecord.Id))]
    public required int? ArtifactId { get; set; } = null;
    public required string? FhirArtifactId { get; set; } = null;
    public int? ConfluencePageId { get; set; } = null;


    public string? ResponsibleWorkGroup { get; set; } = null;
    public string? MaturityLabel { get; set; } = null;
    public int? MaturityLevel { get; set; } = null;
    public string? StandardsStatus { get; set; } = null;

    public ContentDispositionCodes ContentDisposition { get; set; } = ContentDispositionCodes.Unknown;
    public bool? DispositionVotedByWorkgroup { get; set; } = null;
    public string? DispositionLocation { get; set; } = null;

    public bool? ReadyForRemoval { get; set; } = null;

    public string? ManagementComments { get; set; } = null;
    public string? WorkGroupComments { get; set; } = null;


    public required bool? ExistsInPublishIni { get; set; }
    public required bool ExistsInSource { get; set; }


    public int? ConformantShallCount { get; set; } = null;
    public int? ConformantShouldCount { get; set; } = null;
    public int? ConformantMayCount { get; set; } = null;
    public int? ConformantShallNotCount { get; set; } = null;
    public int? ConformantShouldNotCount { get; set; } = null;
    public int? ConformantMayNotCount { get; set; } = null;
    public int? ConformantTotalCount { get; set; } = null;

    public int? NonConformantShallCount { get; set; } = null;
    public int? NonConformantShouldCount { get; set; } = null;
    public int? NonConformantMayCount { get; set; } = null;
    public int? NonConformantShallNotCount { get; set; } = null;
    public int? NonConformantShouldNotCount { get; set; } = null;
    public int? NonConformantMayNotCount { get; set; } = null;
    public int? NonConformantTotalCount { get; set; } = null;

    public int? RemovedFhirArtifactCount { get; set; } = null;

    public int? UnknownWordCount { get; set; } = null;
    public int? TypoWordCount { get; set; } = null;

    public int? PriorFhirVersionReferenceCount { get; set; } = null;

    public int? ImagesWithIssuesCount { get; set; } = null;

    public List<string>? PossibleIncompleteMarkers { get; set; } = null;
    public List<string>? ReaderReviewNotes { get; set; } = null;
    public int? StuLiteralsCount { get; set; } = null;
    public int? DeprecatedLiteralCount { get; set; } = null;

    public int? ZulipLinkCount { get; set; } = null;
    public int? ConfluenceLinkCount { get; set; } = null;
}

[JfSQLiteTable("page_images")]
public partial record class SpecPageImageRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; } = -1;

    [JfSQLiteForeignKey("pages", nameof(SpecPageRecord.Id))]
    public required int PageId { get; set; }

    public required string Source { get; set; }
    public required bool MissingAlt { get; set; }
    public required bool NotInFigure { get; set; }
}


[JfSQLiteTable("page_removed_fhir_artifacts")]
[JfSQLiteIndex(nameof(PageId), nameof(Word))]
public partial record class SpecPageRemovedFhirArtifactRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; } = -1;

    [JfSQLiteForeignKey("pages", nameof(SpecPageRecord.Id))]
    public required int PageId { get; set; }

    public required string Word { get; set; }
    public required string ArtifactClass { get; set; }
}

[JfSQLiteTable("page_unknown_words")]
public partial record class SpecPageUnknownWordRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; } = -1;

    [JfSQLiteForeignKey("pages", nameof(SpecPageRecord.Id))]
    public required int PageId { get; set; }

    public required string Word { get; set; }
    public required bool IsTypo { get; set; }
}

[JfSQLiteTable("artifacts")]
[JfSQLiteIndex(nameof(Name))]
public partial record class ArtifactRecord
{
    [JfSQLiteKey]
    public required int Id { get; set; } = -1;

    [JfSQLiteUnique]
    public required string FhirId { get; set; }

    public int? ConfluencePageId { get; set; } = null;

    public required string Name { get; set; }
    public string? DefinitionArtifactType { get; set; } = null;
    public string? ArtifactType { get; set; } = null;

    public bool? SourceDirectoryExists { get; set; } = null;
    public bool? SourceDefinitionExists { get; set; } = null;

    public string? ResponsibleWorkGroup { get; set; } = null;
    public string? Status { get; set; } = null;
    public int? MaturityLevel { get; set; } = null;
    public string? StandardsStatus { get; set; } = null;

    public ContentDispositionCodes ContentDisposition { get; set; } = ContentDispositionCodes.Unknown;
    public bool? DispositionVotedByWorkgroup { get; set; } = null;
    public string? DispositionLocation { get; set; } = null;

    public bool? ReadyForRemoval { get; set; } = null;

    public string? ManagementComments { get; set; } = null;
    public string? WorkGroupComments { get; set; } = null;


    public string? IntroPageFilename { get; set; } = null;
    public string? NotesPageFilename { get; set; } = null;
}

[JfSQLiteTable("fmg_feedback_sheet")]
[JfSQLiteIndex(nameof(Name))]
public partial record class FmgSheetContentRecord
{
    public enum TrackCodes
    {
        Unknown = 0,
        Normative = 1,
        Informative = 2,
        MoveOutOfCore = 3,
    }

    [JfSQLiteKey]
    public int Id { get; set; } = -1;

    [JfSQLiteUnique]
    [JsonPropertyName("Resource")]
    public required string Name { get; set; }

    [JsonPropertyName("WG")]
    public string? WorkGroupCode { get; set; } = null;

    [JsonPropertyName("FMG Recommendation")]
    public string? FmgRecommendation { get; set; } = null;

    [JsonPropertyName("Track")]
    public string? Track { get; set; } = null;

    [JsonPropertyName("VotedByWg")]
    public string? VotedByWorkgroup { get; set; } = null;

    [JsonPropertyName("WG to FMG")]
    public string? WgToFmg { get; set; } = null;

    [JsonPropertyName("Vote")]
    public string? Vote { get; set; } = null;

    [JsonPropertyName("Notes")]
    public string? Notes { get; set; } = null;

    [JsonPropertyName("Target")]
    public string? Target { get; set; } = null;
}