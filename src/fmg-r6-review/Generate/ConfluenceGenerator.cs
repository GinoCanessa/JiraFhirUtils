using JiraFhirUtils.Common;
using JiraFhirUtils.Common.FhirDbModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace fmg_r6_review.Generate;

public class ConfluenceGenerator
{
    private const string _missingWgCode = "_na";
    private const string _missingWgTitle = "_WORKGROUP NOT SPECIFIED";

    private CliConfig _config;
    private IDbConnection _db = null!;
    private IDbConnection _ciDb = null!;
    private HttpClient? _httpClient = null;
    private int? _wgNotSpecifiedPageId = null;

    internal static readonly HashSet<string> _otherCodes = [
        "OTHER",
        "Other",
        "other",
        "OTH",      // v3 Null Flavor of other
        ];

    internal static readonly HashSet<string> _unknownCodes = [
        "UNKNOWN",
        "Unknown",
        "unknown",
        "UNK",      // v3 Null Flavor of Unknown
        //"NI",       // v3 Null Flavor of No Information
        ];

    public ConfluenceGenerator(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(_config.DbPath))
        {
            throw new ArgumentException("DbPath must be provided.");
        }

        if (string.IsNullOrEmpty(_config.FhirDatabaseCi) ||
            !File.Exists(_config.FhirDatabaseCi))
        {
            throw new ArgumentException("FhirDatabaseCi must be provided and exist.");
        }

        if ((_config.ConfluenceBaseUrl is not null) &&
            (_config.ConfluenceSpaceKey is not null) &&
            (_config.ConfluencePersonalAccessToken is not null))
        {
            _httpClient = new HttpClient();

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ConfluencePersonalAccessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            if (_config.ConfluenceUserAgent is not null)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_config.ConfluenceUserAgent);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new System.Net.Http.Headers.ProductInfoHeaderValue("Mozilla", "5.0"));
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new System.Net.Http.Headers.ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new System.Net.Http.Headers.ProductInfoHeaderValue("AppleWebKit", "537.36"));
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new System.Net.Http.Headers.ProductInfoHeaderValue("(KHTML, like Gecko)"));
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new System.Net.Http.Headers.ProductInfoHeaderValue("Chrome", "139.0.0.0"));
            }

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Atlassian-Token", "no-check");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", new Uri(_config.ConfluenceBaseUrl).AbsoluteUri);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", new Uri(_config.ConfluenceBaseUrl).AbsoluteUri);
        }

    }

    private Dictionary<string, List<(string title, string relativeLink)>> _writtenPages = [];

    public void Generate()
    {
        Console.WriteLine($"Using FMG Review Database: {_config.DbPath}...");
        
        using IDbConnection db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DbPath}");
        db.Open();
        _db = db;

        Console.WriteLine($"Using FHIR CI Database: {_config.FhirDatabaseCi}...");
        
        using IDbConnection? ciDb = (!string.IsNullOrEmpty(_config.FhirDatabaseCi) && File.Exists(_config.FhirDatabaseCi)) ?
            new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.FhirDatabaseCi}") :
            null;
        if (ciDb is null)
        {
            throw new InvalidOperationException("CI FHIR database is required to process artifacts.");
        }
        ciDb.Open();
        _ciDb = ciDb;

        // generate workgroup root pages
        generateWorkgroupRootPages();

        // process pages
        generatePageContent();

        // process artifacts
        generateArtifactContent();

        if (_config.LocalExportDir is not null)
        {
            StringBuilder indexSb = new();

            indexSb.AppendLine("<h1>R6 Review Exported Content Index</h1>");

            // process workgroups in alphabetical order
            foreach ((string wgCode, List<(string title, string relativeLink)> wgPages) in _writtenPages.OrderBy(kvp => kvp.Key))
            {
                // split pages into ones that are in root or in directory
                indexSb.AppendLine($"<h2>Workgroup: {wgCode}</h2>");

                indexSb.AppendLine("<ul>");
                foreach ((string title, string relativeLink) in wgPages.Where(v => v.relativeLink.Contains('/') == false).OrderBy(p => p.title))
                {
                    indexSb.AppendLine($"    <li><a href='{relativeLink}'>{title}</a></li>");
                }

                indexSb.AppendLine("<br/>");

                foreach ((string title, string relativeLink) in wgPages.Where(v => v.relativeLink.Contains('/')).OrderBy(p => p.title))
                {
                    indexSb.AppendLine($"    <li><a href='{relativeLink}'>{title}</a></li>");
                }

                indexSb.AppendLine("</ul>");
            }
            writeToFile("index.html", indexSb.ToString());
            Console.WriteLine($"Exported content to local directory: {_config.LocalExportDir}");
        }

        // clean up
        _db.Close();
        _db = null!;
        _ciDb.Close();
        _ciDb = null!;
    }

    private void generateArtifactContent()
    {
        // get the artifacts from the database
        List<ArtifactRecord> artifacts = ArtifactRecord.SelectList(_db, orderByProperties:[nameof(ArtifactRecord.Id)]);

        // iterate over artifacts and create the confluence content
        foreach (ArtifactRecord artifact in artifacts)
        {
            processArtifact(artifact);
        }
    }

    private void processArtifact(ArtifactRecord artifact)
    {
        // look up the artifact in the CI database to get additional information
        if (_ciDb is null)
        {
            return;
        }

        // act depending on artifact type
        switch (artifact.ArtifactType?.ToLowerInvariant())
        {
            case "primitiveType":
                break;
            case "complexType":
                break;
            case "extension":
            case "resource":
            case "interface":
            case "profile":
                generateStructureContent(artifact);
                break;

            default:
                break;
        }
    }

    private string getStructureElementResultContent(ArtifactRecord artifact, CgDbStructure structure)
    {
        // get the list of elements, sorted by order
        List<CgDbElement> elements = CgDbElement.SelectList(
            _ciDb, 
            StructureKey: structure.Key, 
            IsInherited: false,
            orderByProperties: [nameof(CgDbElement.ResourceFieldOrder)]);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<table>");
        sb.AppendLine("    <thead>");
        sb.AppendLine("        <tr>");
        sb.AppendLine("            <th>Path</th>");
        sb.AppendLine("            <th>Is Required</th>");
        sb.AppendLine("            <th>Not Array</th>");
        sb.AppendLine("            <th>Trial Use</th>");
        sb.AppendLine("            <th>Has fixed[x]</th>");
        sb.AppendLine("            <th>Has pattern[x]</th>");
        sb.AppendLine("            <th>Required Binding</th>");
        sb.AppendLine("            <th>External Required Binding</th>");
        sb.AppendLine("            <th>Check <code>meaningWhenMissing</code></th>");
        sb.AppendLine("            <th>Is Modifier</th>");
        //sb.AppendLine("            <th></th>");
        //sb.AppendLine("            <th></th>");
        //sb.AppendLine("            <th></th>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("    </thead>");
        sb.AppendLine("    <tbody>");

        foreach (CgDbElement element in elements)
        {
            string? requiredBinding = (element.ValueSetBindingStrength?.Equals("required", StringComparison.OrdinalIgnoreCase) == true)
                ? "X"
                : null;
            
            if ((requiredBinding is not null) &&
                (element.BindingValueSetKey is not null))
            {
                // check to see if there is an escape code in the VS
                List<CgDbValueSetConcept> concepts = CgDbValueSetConcept.SelectList(
                    _ciDb,
                    ValueSetKey: element.BindingValueSetKey.Value);

                // check to see if there is an 'other' code
                bool hasOther = concepts.Any(c => _otherCodes.Contains(c.Code));

                // check to see if there is an 'unknown' code
                bool hasUnknown = concepts.Any(c => _unknownCodes.Contains(c.Code));

                if (hasOther && hasUnknown)
                {
                    // both other and unknown are present, just
                    requiredBinding = "Has 'other' and 'unknown'";
                }
                else if (hasOther || hasUnknown)
                {
                    // one of the two is present, so we can flag this
                    requiredBinding = hasOther ? "Has 'other'" : "Has 'unknown'";
                }
                else
                {
                    // neither is present, so we cannot flag this
                    requiredBinding = "NO 'other' or 'unknown'";
                }
            }

            if ((requiredBinding is not null) &&
                (element.MinCardinality == 0))
            {
                requiredBinding = "<i>optional</i> - " + requiredBinding;
            }

            requiredBinding ??= string.Empty;

            string? externalRequiredBinding =
                (element.ValueSetBindingStrength?.Equals("required", StringComparison.OrdinalIgnoreCase) == true) &&
                (element.BindingValueSet?.StartsWith("http://hl7.org/", StringComparison.OrdinalIgnoreCase) != true)
                ? element.BindingValueSet
                : null;

            string shouldAddMeaningWhenMissing = 
                (element.MinCardinality == 0) && (element.MeaningWhenMissing is null) && 
                (element.FullCollatedTypeLiteral.Contains("code", StringComparison.Ordinal) || element.FullCollatedTypeLiteral.Contains("boolean"))
                ? "X"
                : string.Empty;

            sb.AppendLine("        <tr>");
            sb.AppendLine($"            <td><code>{element.Path}</code></td>");
            sb.AppendLine($"            <td>{(element.MinCardinality > 0 ? "X" : string.Empty)}</td>");
            sb.AppendLine($"            <td>{(element.MaxCardinality != 1 ? "X" : string.Empty)}</td>");
            sb.AppendLine($"            <td>{(element.StandardStatus?.Equals("trial-use", StringComparison.OrdinalIgnoreCase) == true ? "X" : string.Empty)}</td>");
            sb.AppendLine($"            <td>{(element.FixedValue is null ? string.Empty : "X")}</td>");
            sb.AppendLine($"            <td>{(element.PatternValue is null ? string.Empty : "X")}</td>");
            sb.AppendLine($"            <td>{requiredBinding}</td>");
            sb.AppendLine($"            <td><code>{externalRequiredBinding}</code></td>");
            sb.AppendLine($"            <td>{shouldAddMeaningWhenMissing}</td>");
            sb.AppendLine($"            <td>{(element.IsModifier ? (string.IsNullOrEmpty(element.IsModifierReason) ? "NO REASON" : "X") : string.Empty)}</td>");
            sb.AppendLine("        </tr>");
        }

        sb.AppendLine("    </tbody>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    private void generateStructureContent(ArtifactRecord artifact)
    {
        HashSet<string> pageTaskIds = [];

        // get the structure from the CI database
        CgDbStructure? structure = CgDbStructure.SelectSingle(
            _ciDb,
            Id: artifact.FhirId);

        if (structure is null)
        {
            throw new InvalidOperationException($"Could not find structure with Id '{artifact.FhirId}' in CI database.");
        }

        string wgCode = artifact.ResponsibleWorkGroup ?? _missingWgCode;
        string pageName = artifact.FhirId;

        SpecPageRecord? infoPage = artifact.IntroPageFilename is null
            ? null
            : SpecPageRecord.SelectSingle(_db, PageFileName: artifact.IntroPageFilename);

        SpecPageRecord? notesPage = artifact.NotesPageFilename is null
            ? null
            : SpecPageRecord.SelectSingle(_db, PageFileName: artifact.NotesPageFilename);

        string content = $$$"""
            <ac:structured-macro ac:name="panel">
                <ac:parameter ac:name="title">R6 Checklist for {{{artifact.FhirId}}}</ac:parameter>
                <ac:rich-text-body>
                    <h2>Artifact {{{artifact.FhirId}}} ({{{artifact.Name}}})</h2>

                    <h3>Urgent Item Checklist</h3>
                    {{{getStructureUrgentChecklist(artifact, structure, pageTaskIds)}}}

                    <h2>All Analysis Information</h2>
                    The following content was generated to assist in the review of this artifact for R6 compliance.

                    <h3>Information Page</h3>
                    {{{(infoPage is null ? "Not defined!" : getPageResultContent(infoPage))}}}

                    <h3>Notes Page</h3>
                    {{{(notesPage is null ? "Not defined!" : getPageResultContent(notesPage))}}}

                    <h3>Element Review</h3>
                    {{{getStructureElementResultContent(artifact, structure)}}}

                    <h3>Operations</h3>
                    {{{getStructureOperationContent(artifact, structure)}}}

                    <h3>Search Parameters</h3>
                    {{{getStructureSearchParameterContent(artifact, structure)}}}
                </ac:rich-text-body>
            </ac:structured-macro>
            """;

        if (_config.LocalExportDir is not null)
        {
            string filename = $"sd_{pageName}.html";
            writeToFile(filename, content, Path.Combine(_config.LocalExportDir, wgCode));
            if (!_writtenPages.TryGetValue(wgCode, out List<(string title, string relativeLink)>? wgPageList))
            {
                wgPageList = [];
                _writtenPages[wgCode] = wgPageList;
            }
            wgPageList.Add((structure.ArtifactClass.ToString() + " " + structure.Id, Path.Combine(wgCode, filename)));
        }

        //if ((_config.ConfluenceBaseUrl is not null) &&
        //    (_config.ConfluenceSpaceKey is not null) &&
        //    (_config.ConfluencePersonalAccessToken is not null) &&
        //    (_config.ConfluenceRootPageId is not null))
        //{
        //    int? pageId = writeToConfluence(_config.ConfluenceRootPageId.Value, workgroup?.ConfluencePageId, wgTitle, content);

        //    if ((workgroup is not null) &&
        //        (pageId is not null) &&
        //        (pageId != workgroup.ConfluencePageId))
        //    {
        //        workgroup.ConfluencePageId = pageId;
        //        workgroup.Update(_db);
        //    }
        //    else if ((workgroup is null) &&
        //             (pageId is not null) &&
        //             (pageId != _wgNotSpecifiedPageId))
        //    {
        //        _wgNotSpecifiedPageId = pageId;
        //    }
        //}
    }

    private string getPageUrgentChecklist(
        SpecPageRecord page,
        HashSet<string> pageTaskIds)
    {
        // TO DO: need to build list of urgent tasks for pages (e.g., informative pages need conformance language removed)
        return string.Empty;
    }

    private string getStructureUrgentChecklist(
        ArtifactRecord artifact, 
        CgDbStructure structure,
        HashSet<string> pageTaskIds)
    {
        StringBuilder sb = new StringBuilder();

        //sb.AppendLine("        <ac:task-list>");
        sb.AppendLine("        <ul>");

        // necessary tasks depend on the disposition
        switch (artifact.ContentDisposition)
        {
            case ContentDispositionCodes.Unknown:
                addTasksForDispoUnknown(sb, artifact, structure, pageTaskIds);
                break;
            case ContentDispositionCodes.CoreAsNormative:
                addTasksForDispoCoreNormative(sb, artifact, structure, pageTaskIds);
                break;
            case ContentDispositionCodes.MoveToGuide:
                addTasksForDispoMoveToGuide(sb, artifact, structure, pageTaskIds);
                break;
            case ContentDispositionCodes.Remove:
                addTasksForDispoRemove(sb, artifact, structure, pageTaskIds);
                break;

            // invalid states for artifacts
            case ContentDispositionCodes.CoreAsInformative:
            case ContentDispositionCodes.MoveToConfluence:
                sb.AppendLine($"            <li><strong>ERROR:</strong> Invalid content disposition for core specification artifact: <code>{artifact.ContentDisposition.ToString()}</code>!</li>");
                break;
            default:
                break;
        }

        //sb.AppendLine("        </ac:task-list>");
        sb.AppendLine("        </ul>");


        return sb.ToString();
    }

    private void addTasksForDispoRemove(
        StringBuilder sb,
        ArtifactRecord artifact,
        CgDbStructure structure,
        HashSet<string> pageTaskIds)
    {
        if (artifact.DispositionVotedByWorkgroup != true)
        {
            sb.AppendLine("            <li>Confirm workgroup disposition vote has been recorded and sent to FMG!</li>");
        }

        if (artifact.ReadyForRemoval != true)
        {
            sb.AppendLine("            <li>Prepare content for removal from core specification</li>");
            sb.AppendLine("            <li>Confirm content is ready for removal from core specification and notify FMG</li>");
        }
    }

    private void addTasksForDispoMoveToGuide(
        StringBuilder sb,
        ArtifactRecord artifact,
        CgDbStructure structure,
        HashSet<string> pageTaskIds)
    {
        if (artifact.DispositionVotedByWorkgroup != true)
        {
            sb.AppendLine("            <li>Confirm workgroup disposition vote has been recorded and sent to FMG!</li>");
        }

        if (string.IsNullOrEmpty(artifact.DispositionLocation))
        {
            sb.AppendLine("            <li>Specify location for relocation of content to external guide</li>");
        }

        if (artifact.ReadyForRemoval != true)
        {
            sb.AppendLine("            <li>Prepare content for relocation to external guide</li>");
            sb.AppendLine("            <li>Confirm content is ready for removal from core specification and notify FMG</li>");
        }
    }

    private void addTasksForDispoUnknown(
        StringBuilder sb,
        ArtifactRecord artifact,
        CgDbStructure structure,
        HashSet<string> pageTaskIds)
    {
        sb.AppendLine("            <li>Vote on and report content disposition to FMG: Core as Normative, Relocate, or Remove</li>");
        sb.AppendLine("            <p>Note that the following list will be based on remaining in Core as Normative.");

        addTasksForDispoCoreNormative(sb, artifact, structure, pageTaskIds);
    }

    private void addTasksForDispoCoreNormative(
        StringBuilder sb,
        ArtifactRecord artifact,
        CgDbStructure structure,
        HashSet<string> pageTaskIds)
    {
        if (artifact.DispositionVotedByWorkgroup != true)
        {
            sb.AppendLine("            <li>Confirm workgroup disposition vote has been recorded and sent to FMG!</li>");
        }

        // find elements flagged as trial-use - filter separately to capture issues with casing and hyphenation
        List<CgDbElement> elements = CgDbElement.SelectList(
                _ciDb,
                StructureKey: structure.Key,
                IsInherited: false,
                orderByProperties: [nameof(CgDbElement.ResourceFieldOrder)])
            .Where(e => e.StandardStatus?.StartsWith("trial", StringComparison.OrdinalIgnoreCase) ?? false)
            .ToList();

        foreach (CgDbElement element in elements)
        {
            sb.AppendLine($"            <li>Element: <code>{element.Id}</code> flagged as <code>trial-use</code></li>");
        }

        // find elements flagged as deprecated
        elements = CgDbElement.SelectList(
                _ciDb,
                StructureKey: structure.Key,
                IsInherited: false,
                orderByProperties: [nameof(CgDbElement.ResourceFieldOrder)])
            .Where(e => e.StandardStatus?.Equals("deprecated", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (CgDbElement element in elements)
        {
            sb.AppendLine($"            <li>Element: <code>{element.Id}</code> flagged as <code>deprecated</code></li>");
        }

        // review ValueSets and CodeSystems based bindings
        elements = CgDbElement.SelectList(
                _ciDb,
                StructureKey: structure.Key,
                IsInherited: false,
                BindingValueSetKeyIsNull: false,
                orderByProperties: [nameof(CgDbElement.ResourceFieldOrder)]);

        HashSet<int> processedValueSetKeys = [];
        HashSet<int> processedCodeSystemKeys = [];
        foreach (CgDbElement element in elements)
        {
            if ((element.BindingValueSetKey is null) ||
                !processedValueSetKeys.Add(element.BindingValueSetKey.Value))
            {
                continue;
            }

            // resolve the ValueSet
            CgDbValueSet? valueSet = CgDbValueSet.SelectSingle(
                _ciDb,
                Key: element.BindingValueSetKey.Value);

            if (valueSet is null)
            {
                continue;
            }

            // if (valueSet.Status?.Equals("draft", StringComparison.OrdinalIgnoreCase) == true)
            // {
            //     sb.AppendLine($"            <li>ValueSet: <code>{valueSet.Id}</code> ({valueSet.Name}) bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) has status <code>{valueSet.Status}</code></li>");
            // }
            
            // if ((valueSet.StandardStatus?.StartsWith("trial", StringComparison.OrdinalIgnoreCase) == true) ||
            //     (valueSet.StandardStatus?.Equals("informative", StringComparison.OrdinalIgnoreCase) == true) ||
            //     (valueSet.StandardStatus?.Equals("deprecated", StringComparison.OrdinalIgnoreCase) == true))
            // {
            //     sb.AppendLine($"            <li>ValueSet: <code>{valueSet.Id}</code> ({valueSet.Name}) bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) has standard status <code>{valueSet.StandardStatus}</code></li>");
            // }

            if (valueSet.IsExperimental == true)
            {
                sb.AppendLine($"            <li>ValueSet: <code>{valueSet.Id}</code> ({valueSet.Name}) bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) is flagged as <code>experimental</code></li>");
            }

            string[] systems = valueSet.ReferencedSystems?.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            foreach (string system in systems)
            {
                // resolve the CodeSystem
                CgDbCodeSystem? codeSystem = system.Contains('|')
                    ? CgDbCodeSystem.SelectSingle(_ciDb, VersionedUrl: system)
                    : CgDbCodeSystem.SelectSingle(_ciDb, UnversionedUrl: system);
                if ((codeSystem is null) ||
                    !processedCodeSystemKeys.Add(codeSystem.Key))
                {
                    continue;
                }

                // if (codeSystem.Status?.Equals("draft", StringComparison.OrdinalIgnoreCase) == true)
                // {
                //     sb.AppendLine($"            <li>CodeSystem: <code>{codeSystem.Id}</code> ({codeSystem.Name}) referenced by ValueSet <code>{valueSet.Id}</code> bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) has status <code>{codeSystem.Status}</code></li>");
                // }

                // if ((codeSystem.StandardStatus?.StartsWith("trial", StringComparison.OrdinalIgnoreCase) == true) ||
                //     (codeSystem.StandardStatus?.Equals("informative", StringComparison.OrdinalIgnoreCase) == true) ||
                //     (codeSystem.StandardStatus?.Equals("deprecated", StringComparison.OrdinalIgnoreCase) == true))
                // {
                //     sb.AppendLine($"            <li>CodeSystem: <code>{codeSystem.Id}</code> ({codeSystem.Name}) referenced by ValueSet <code>{valueSet.Id}</code> bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) has standard status <code>{codeSystem.StandardStatus}</code></li>");
                // }

                if (codeSystem.IsExperimental == true)
                {
                    sb.AppendLine($"            <li>CodeSystem: <code>{codeSystem.Id}</code> ({codeSystem.Name}) referenced by ValueSet <code>{valueSet.Id}</code> bound to element <code>{element.Id}</code> (<code>{element.ValueSetBindingStrength}</code>) is flagged as <code>experimental</code></li>");
                }
                
                // get the list of all value sets that reference this code system
                List<CgDbValueSetSystem> vsSystems = CgDbValueSetSystem.SelectList(
                    _ciDb,
                    PackageKey: codeSystem.PackageKey,
                    CodeSystemKey: codeSystem.Key);

                bool hasCodeBinding = false;

                // iterate over the value sets
                foreach (CgDbValueSetSystem vsSystem in vsSystems)
                {
                    // resolve this value set
                    CgDbValueSet? csVs = CgDbValueSet.SelectSingle(_ciDb, Key: vsSystem.ValueSetKey);

                    if (csVs is null)
                    {
                        continue;
                    }
                    
                    // check to see if there are NO code-type bindings
                    if ((csVs.StrongestBindingCoreCode is null) &&
                        (csVs.StrongestBindingExtendedCode is null))
                    {
                        continue;
                    }
                    
                    hasCodeBinding = true;
                    break;
                }
                
                // if there are no code bindings, suggest moving to THO
                if (!hasCodeBinding)
                {
                    sb.AppendLine($"            <li>CodeSystem: <code>{codeSystem.Id}</code> ({codeSystem.Name}) referenced by ValueSet <code>{valueSet.Id}</code> bound to element <code>{element.Id}</code> is never bound to a <code>code</code>-type element. Should it move to THO?</li>");
                }
            }
        }

        // get all operations for this resource
        List<CgDbOperation> operations = CgDbOperation.SelectList(_ciDb, orderByProperties:[nameof(CgDbOperation.Id)])
            .Where(op => 
                op.ResourceTypeList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase)) ||
                op.AdditionalResourceTypeList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase)) ||
                op.Id.StartsWith(structure.Id + "-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        // // find operations in status draft
        // foreach (CgDbOperation op in operations.Where(op => op.Status?.Equals("draft", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Operation: <code>{op.Id}</code> ({op.Name}) has status of <code>{op.Status}</code></li>");
        // }
        //
        // // find operations flagged as trial use
        // foreach (CgDbOperation operation in operations.Where(op => op.StandardStatus?.StartsWith("trial", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Operation: <code>{operation.Id}</code> ({operation.Name}) has standards status <code>{operation.StandardStatus}</code></li>");
        // }
        //
        // // find operations flagged as deprecated
        // foreach (CgDbOperation op in operations.Where(op => op.StandardStatus?.Equals("deprecated", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Operation: <code>{op.Id}</code> ({op.Name}) has standards status <code>{op.StandardStatus}</code></li>");
        // }

        // find operations flagged as experimental
        foreach (CgDbOperation op in operations.Where(op => op.IsExperimental == true))
        {
            sb.AppendLine($"            <li>Operation: <code>{op.Id}</code> ({op.Name}) flagged as <code>experimental</code></li>");
        }

        // get all search parameters for this resource
        List<CgDbSearchParameter> searchParameters = CgDbSearchParameter.SelectList(_ciDb, orderByProperties:[nameof(CgDbSearchParameter.Id)])
            .Where(sp => 
                sp.BaseResourceList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase)) ||
                sp.AdditionalBaseResourceList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase)) ||
                sp.Id.StartsWith(structure.Id + "-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // // find search parameters in draft
        // foreach (CgDbSearchParameter sp in searchParameters.Where(sp => sp.Status?.Equals("draft", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Search Parameter: <code>{sp.Id}</code> ({sp.Name}) has status <code>{sp.Status}</code></li>");
        // }
        //
        // // find search parameters flagged as trial use
        // foreach (CgDbSearchParameter sp in searchParameters.Where(sp => sp.StandardStatus?.StartsWith("trial", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Search Parameter: <code>{sp.Id}</code> ({sp.Name}) has standards status <code>{sp.StandardStatus}</code></li>");
        // }
        //
        // // find search parameters flagged as deprecated
        // foreach (CgDbSearchParameter sp in searchParameters.Where(sp => sp.StandardStatus?.Equals("deprecated", StringComparison.OrdinalIgnoreCase) == true))
        // {
        //     sb.AppendLine($"            <li>Search Parameter: <code>{sp.Id}</code> ({sp.Name}) has standards status <code>{sp.StandardStatus}</code></li>");
        // }

        // find search parameters flagged as experimental
        foreach (CgDbSearchParameter sp in searchParameters.Where(sp => sp.IsExperimental == true))
        {
            sb.AppendLine($"            <li>Search Parameter: <code>{sp.Id}</code> ({sp.Name}) flagged as <code>experimental</code></li>");
        }
    }

    private string generateTaskId(HashSet<string> pageTaskIds)
    {
        long seed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string id = seed.ToString();

        while (pageTaskIds.Contains(id))
        {
            seed++;
            id = seed.ToString();
        }

        return id;
    }

    private string getStructureSearchParameterContent(ArtifactRecord artifact, CgDbStructure structure)
    {
        StringBuilder sb = new StringBuilder();

        // get the searchParameters from the structure
        List<CgDbSearchParameter> searchParameters = CgDbSearchParameter.SelectList(_ciDb, orderByProperties:[nameof(CgDbSearchParameter.Id)])
                .Where(sp => 
                    sp.BaseResourceList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase) ||
                    sp.AdditionalBaseResourceList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase) ||
                    sp.Id.StartsWith(structure.Id + "-", StringComparison.Ordinal))))
                .ToList();

        if (searchParameters.Count == 0)
        {
            sb.AppendLine("<p>No search parameters found.</p>");
        }
        else
        {
            sb.AppendLine("<table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("        <tr>");
            sb.AppendLine("            <th>Id</th>");
            sb.AppendLine("            <th>Name</th>");
            sb.AppendLine("            <th>Publication Status</th>");
            sb.AppendLine("            <th>FMM</th>");
            sb.AppendLine("            <th>Standards Status</th>");
            sb.AppendLine("            <th>IsExperimental</th>");
            sb.AppendLine("            <th>WorkGroup</th>");
            sb.AppendLine("            <th>Search Type</th>");
            sb.AppendLine("            <th>Description</th>");
            sb.AppendLine("        </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (CgDbSearchParameter sp in searchParameters)
            {
                sb.AppendLine("        <tr>");
                sb.AppendLine($"            <td><code>{sp.Id}</code></td>");
                sb.AppendLine($"            <td><code>{sp.Name}</code></td>");
                sb.AppendLine($"            <td>{sp.Status}</td>");
                sb.AppendLine($"            <td>{sp.FhirMaturity}</td>");
                sb.AppendLine($"            <td>{sp.StandardStatus}</td>");
                sb.AppendLine($"            <td>{sp.IsExperimental}</td>");
                sb.AppendLine($"            <td>{sp.WorkGroup}</td>");
                sb.AppendLine($"            <td>{sp.SearchType}</td>");
                sb.AppendLine($"            <td>{sp.Description}</td>");
                sb.AppendLine("        </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("</table>");
        }

        return sb.ToString();
    }

    private string getStructureOperationContent(ArtifactRecord artifact, CgDbStructure structure)
    {
        StringBuilder sb = new StringBuilder();

        // get the searchParameters from the structure
        List<CgDbOperation> operations = CgDbOperation.SelectList(_ciDb, orderByProperties:[nameof(CgDbOperation.Id)])
                .Where(op => 
                    op.ResourceTypeList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase) ||
                    op.AdditionalResourceTypeList.Any(r => r.Equals(structure.Name, StringComparison.OrdinalIgnoreCase) ||
                    op.Id.StartsWith(structure.Id + "-", StringComparison.Ordinal))))
                .ToList();

        if (operations.Count == 0)
        {
            sb.AppendLine("<p>No operations found.</p>");
        }
        else
        {
            sb.AppendLine("<table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("        <tr>");
            sb.AppendLine("            <th>Id</th>");
            sb.AppendLine("            <th>Name</th>");
            sb.AppendLine("            <th>Publication Status</th>");
            sb.AppendLine("            <th>FMM</th>");
            sb.AppendLine("            <th>Standards Status</th>");
            sb.AppendLine("            <th>IsExperimental</th>");
            sb.AppendLine("            <th>Workgroup</th>");
            sb.AppendLine("            <th>Description</th>");
            sb.AppendLine("        </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (CgDbOperation operation in operations)
            {
                sb.AppendLine("        <tr>");
                sb.AppendLine($"            <td><code>{operation.Id}</code></td>");
                sb.AppendLine($"            <td><code>{operation.Name}</code></td>");
                sb.AppendLine($"            <td>{operation.Status}</td>");
                sb.AppendLine($"            <td>{operation.FhirMaturity}</td>");
                sb.AppendLine($"            <td>{operation.StandardStatus}</td>");
                sb.AppendLine($"            <td>{operation.IsExperimental}</td>");
                sb.AppendLine($"            <td>{operation.WorkGroup}</td>");
                sb.AppendLine($"            <td>{operation.Description}</td>");
                sb.AppendLine("        </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("</table>");
        }

        return sb.ToString();
    }

    private void generatePageContent()
    {
        // get the specification pages that are not tied to artifacts
        List<SpecPageRecord> specPages = SpecPageRecord.SelectList(_db, FhirArtifactIdIsNull: true);

        // iterate over pages and create the confluence content
        foreach (SpecPageRecord specPage in specPages)
        {
            genSpecPageContent(specPage);
        }
    }

    private string getPageResultContent(SpecPageRecord page)
    {
        string wgCode = page.ResponsibleWorkGroup ?? _missingWgCode;

        // resolve related lists
        List<SpecPageRemovedFhirArtifactRecord> removedFhirArtifactRecords = SpecPageRemovedFhirArtifactRecord.SelectList(_db, PageId: page.Id, orderByProperties:[nameof(SpecPageRemovedFhirArtifactRecord.Word)]);
        List<SpecPageUnknownWordRecord> unknownWordRecords = SpecPageUnknownWordRecord.SelectList(_db, PageId: page.Id, orderByProperties:[nameof(SpecPageUnknownWordRecord.Word)]);
        List<SpecPageImageRecord> imgIssueRecords = SpecPageImageRecord.SelectList(_db, PageId: page.Id, orderByProperties:[nameof(SpecPageImageRecord.Source)]);

        string content = $$$"""
                    <h3>General Information:</h3>
                    <table>
                        <tbody>
                            <tr>
                                <td>Page File Name</td>
                                <td><code>{{{page.PageFileName}}}</code></td>
                            </tr>
                            <tr>
                                <td>Responsible Workgroup</td>
                                <td><code>{{{page.ResponsibleWorkGroup ?? string.Empty}}}</code></td>
                            </tr>
                            <tr>
                                <td>Maturity Label</td>
                                <td><code>{{{page.MaturityLabel}}}</code></td>
                            </tr>
                            <tr>
                                <td>Maturity Level</td>
                                <td><code>{{{page.MaturityLevel}}}</code></td>
                            </tr>
                            <tr>
                                <td>Standards Status</td>
                                <td><code>{{{page.StandardsStatus}}}</code></td>
                            </tr>
                            <tr>
                                <td>Management Comments</td>
                                <td>{{{page.ManagementComments}}}</td>
                            </tr>
                            <tr>
                                <td>Workgroup Comments</td>
                                <td>{{{page.WorkGroupComments}}}</td>
                            </tr>
                            <tr>
                                <td>Exists In <code>publish.ini</code></td>
                                <td><code>{{{(page.ExistsInPublishIni == true ? "Yes" : "No")}}}</code></td>
                            </tr>
                            <tr>
                                <td>Exists In Source Directory</td>
                                <td><code>{{{(page.ExistsInSource == true ? "Yes" : "No")}}}</code></td>
                            </tr>
                            <tr>
                                <td>'<code>deprecated</code>' Literal Count</td>
                                <td><code>{{{page.DeprecatedLiteralCount ?? 0}}}</code></td>
                            </tr>
                            <tr>
                                <td>Zulip Link Count</td>
                                <td><code>{{{page.ZulipLinkCount ?? 0}}}</code></td>
                            </tr>
                            <tr>
                                <td>Confluence Link Count</td>
                                <td><code>{{{page.ConfluenceLinkCount ?? 0}}}</code></td>
                            </tr>
                        </tbody>
                    </table>


                    <h3>Conformance Language Summary</h3>
                    <p>'Conformant' conformance language appears in all-upper case. 'Non-conformant' covers all other cases appearing in the content.</p>
                    <table>
                        <thead>
                            <tr>
                                <th>Literal</th>
                                <th>Conformant</th>
                                <th>Non-Conformant</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td><b>SHALL</b></td>
                                <td>{{{page.ConformantShallCount}}}</td>
                                <td>{{{page.NonConformantShallCount}}}</td>
                            </tr>
                            <tr>
                                <td><b>SHALL NOT</b></td>
                                <td>{{{page.ConformantShallNotCount}}}</td>
                                <td>{{{page.NonConformantShallNotCount}}}</td>
                            </tr>
                            <tr>
                                <td><b>SHOULD</b></td>
                                <td>{{{page.ConformantShouldCount}}}</td>
                                <td>{{{page.NonConformantShouldCount}}}</td>
                            </tr>
                            <tr>
                                <td><b>SHOULD NOT</b></td>
                                <td>{{{page.ConformantShouldNotCount}}}</td>
                                <td>{{{page.NonConformantShouldNotCount}}}</td>
                            </tr>
                            <tr>
                                <td><b>MAY</b></td>
                                <td>{{{page.ConformantMayCount}}}</td>
                                <td>{{{page.NonConformantMayCount}}}</td>
                            </tr>
                            <tr>
                                <td><b>MAY NOT</b></td>
                                <td>{{{page.ConformantMayNotCount}}}</td>
                                <td>{{{page.NonConformantMayNotCount}}}</td>
                            </tr>
                        </tbody>
                    </table>

                    <h3>Possibly Removed FHIR Artifact Literals Found on Page</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>Word</th>
                                <th>Artifact Class</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{{string.Join("\n            ", removedFhirArtifactRecords.Select(record =>
                                $"<tr>" +
                                $"<td><code>{record.Word}<code></td>" +
                                $"<td>{record.ArtifactClass}</td>" +
                                $"</tr>"
                            ))}}}
                        </tbody>
                    </table>

                    <h3>Unknown Words Found on Page</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>Word</th>
                                <th>Known Typo</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{{string.Join("\n            ", unknownWordRecords.Select(record =>
                                $"<tr>" +
                                $"<td><code>{record.Word}<code></td>" +
                                $"<td>{(record.IsTypo == true ? "Yes" : "No")}</td>" +
                                $"</tr>"
                            ))}}}
                        </tbody>
                    </table>
            
                    <h3>Images with Issues</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>Source</th>
                                <th>Missing Alt Text</th>
                                <th>Not In Figure Tag</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{{string.Join("\n            ", imgIssueRecords.Select(record =>
                                $"<tr>" +
                                $"<td><code>{record.Source}<code></td>" +
                                $"<td>{(record.MissingAlt ? "Yes" : "No")}</td>" +
                                $"<td>{(record.NotInFigure ? "Yes" : "No")}</td>" +
                                $"</tr>"
                            ))}}}
                        </tbody>
                    </table>

                    <h3>Possible Incomplete Markers Found on Page</h3>
                    <ul>
                        {{{string.Join(
                            "\n            ",
                            page.PossibleIncompleteMarkers?.Select(marker => $"<li><code>{marker}</code></li>") ?? [])}}}
                    </ul>

                    <h3>Reader Review Notes Found on Page</h3>
                    <ul>
                        {{{string.Join(
                            "\n            ",
                            page.ReaderReviewNotes?.Select(note => $"<li>{note}</li>") ?? [])}}}
                    </ul>
            """;

        return content;
    }

    private void genSpecPageContent(SpecPageRecord page)
    {
        string wgCode = page.ResponsibleWorkGroup ?? _missingWgCode;
        string pageName = Path.GetFileNameWithoutExtension(page.PageFileName);

        string content = $$$"""
            <ac:structured-macro ac:name="panel">
                <ac:parameter ac:name="title">R6 Checklist for {{{page.PageFileName}}}</ac:parameter>
                <ac:rich-text-body>
                    <h2>Specification Page: {{{page.PageFileName}}}</h2>

                    {{{getPageResultContent(page)}}}
                </ac:rich-text-body>
            </ac:structured-macro>
            """;

        if (_config.LocalExportDir is not null)
        {
            string filename = $"page_{pageName}.html";
            writeToFile($"page_{pageName}.html", content, Path.Combine(_config.LocalExportDir, wgCode));
            if (!_writtenPages.TryGetValue(wgCode, out List<(string title, string relativeLink)>? wgPageList))
            {
                wgPageList = [];
                _writtenPages[wgCode] = wgPageList;
            }
            wgPageList.Add(("Page: " + pageName, Path.Combine(wgCode, filename)));
        }

        //if ((_config.ConfluenceBaseUrl is not null) &&
        //    (_config.ConfluenceSpaceKey is not null) &&
        //    (_config.ConfluencePersonalAccessToken is not null) &&
        //    (_config.ConfluenceRootPageId is not null))
        //{
        //    int? pageId = writeToConfluence(_config.ConfluenceRootPageId.Value, workgroup?.ConfluencePageId, wgTitle, content);

        //    if ((workgroup is not null) &&
        //        (pageId is not null) &&
        //        (pageId != workgroup.ConfluencePageId))
        //    {
        //        workgroup.ConfluencePageId = pageId;
        //        workgroup.Update(_db);
        //    }
        //    else if ((workgroup is null) &&
        //             (pageId is not null) &&
        //             (pageId != _wgNotSpecifiedPageId))
        //    {
        //        _wgNotSpecifiedPageId = pageId;
        //    }
        //}
    }

    private void generateWorkgroupRootPages()
    {
        // get the workgroups
        List<WorkgroupRecord> workgroups = WorkgroupRecord.SelectList(_db, orderByProperties: [nameof(WorkgroupRecord.Code)]);

        // iterate over workgroups and create the confluence content
        foreach (WorkgroupRecord workgroup in workgroups)
        {
            genWorkgroupRootPage(workgroup);
        }

        // check for unassigned pages/artifacts
        genWorkgroupRootPage(null);
    }

    private void genWorkgroupRootPage(WorkgroupRecord? workgroup)
    {
        string wgCode = workgroup?.Code ?? _missingWgCode;
        string wgTitle = workgroup?.Title ?? _missingWgTitle;

        // build the list of pages for this workgroup
        List<SpecPageRecord> pages = workgroup is null
            ? SpecPageRecord.SelectList(_db, ResponsibleWorkGroupIsNull: true, ArtifactIdIsNull: true, orderByProperties:[nameof(SpecPageRecord.PageFileName)])
            : SpecPageRecord.SelectList(_db, ResponsibleWorkGroup: workgroup.Code, ArtifactIdIsNull: true, orderByProperties:[nameof(SpecPageRecord.PageFileName)]);

        // build the list of artifacts for this workgroup
        List<ArtifactRecord> artifacts = workgroup is null
            ? ArtifactRecord.SelectList(_db, ResponsibleWorkGroupIsNull: true, orderByProperties:[nameof(ArtifactRecord.Id)])
            : ArtifactRecord.SelectList(_db, ResponsibleWorkGroup: workgroup.Code, orderByProperties:[nameof(ArtifactRecord.Id)]);

        // if there are no pages or artifacts, skip this workgroup
        if ((pages.Count == 0) &&
            (artifacts.Count == 0))
        {
            return;
        }

        string content = $$$"""
            <ac:structured-macro ac:name="panel">
                <ac:parameter ac:name="title">R6 Checklist for {{{wgTitle}}}</ac:parameter>

                <ac:rich-text-body>
                    <h2>Workgroup: {{{wgCode}}} - {{{wgTitle}}}</h2>

                    <h3>Specification Pages</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>Page File Name</th>
                                <th>Maturity Label</th>
                                <th>Maturity Level</th>
                                <th>Standards Status</th>
                                <th>Content Disposition</th>
                                <th>Disposition Location</th>
                                <th>Ready For Removal</th>
                                <th>Management Comments</th>
                                <th>Workgroup Comments</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{{string.Join("\n                ", pages.Select(page =>
                                $"<tr>" +
                                $"<td>{page.PageFileName}</td>" +
                                $"<td>{page.MaturityLabel}</td>" +
                                $"<td>{page.MaturityLevel}</td>" +
                                $"<td>{page.StandardsStatus}</td>" +
                                $"<td>{page.ContentDisposition}</td>" +
                                $"<td>{page.DispositionLocation}</td>" +
                                $"<td>{page.ReadyForRemoval}</td>" +
                                $"<td>{page.ManagementComments}</td>" +
                                $"<td>{page.WorkGroupComments}</td>" +
                                $"</tr>"))}}}
                        </tbody>
                    </table>

                    <h3>Artifacts</h3>
                    <table>
                        <thead>
                            <tr>
                                <th>Id</th>
                                <th>Name</th>
                                <th>Artifact Type</th>
                                <th>Status</th>
                                <th>Maturity Level</th>
                                <th>Standards Status</th>
                                <th>Content Disposition</th>
                                <th>Disposition Location</th>
                                <th>Ready For Removal</th>
                                <th>Management Comments</th>
                                <th>Workgroup Comments</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{{string.Join("\n                ", artifacts.Select(artifact =>
                                $"<tr>" +
                                $"<td>{artifact.FhirId}</td>" +
                                $"<td>{artifact.Name}</td>" +
                                $"<td>{artifact.ArtifactType}</td>" +
                                $"<td>{artifact.Status}</td>" +
                                $"<td>{artifact.MaturityLevel}</td>" +
                                $"<td>{artifact.StandardsStatus}</td>" +
                                $"<td>{artifact.ContentDisposition}</td>" +
                                $"<td>{artifact.DispositionLocation}</td>" +
                                $"<td>{artifact.ReadyForRemoval}</td>" +
                                $"<td>{artifact.ManagementComments}</td>" +
                                $"<td>{artifact.WorkGroupComments}</td>" +
                                $"</tr>"))}}}
                        </tbody>

                </ac:rich-text-body>
            </ac:structured-macro>
            """;

        if (_config.LocalExportDir is not null)
        {
            string filename = $"{wgCode}-root-page.html";
            writeToFile(filename, content);
            string wgDir = Path.Combine(_config.LocalExportDir, wgCode);
            if (!_writtenPages.TryGetValue(wgCode, out List<(string title, string relativeLink)>? wgPageList))
            {
                wgPageList = [];
                _writtenPages[wgCode] = wgPageList;
            }
            wgPageList.Add(($" Work Group Listing: {wgCode} - {wgTitle}", filename));

            if (!Directory.Exists(wgDir))
            {
                Directory.CreateDirectory(wgDir);
            }
        }

        if ((_config.ConfluenceBaseUrl is not null) &&
            (_config.ConfluenceSpaceKey is not null) &&
            (_config.ConfluencePersonalAccessToken is not null) &&
            (_config.ConfluenceRootPageId is not null))
        {
            int? pageId = writeToConfluence(_config.ConfluenceRootPageId.Value, workgroup?.ConfluencePageId, wgTitle, content);

            if ((workgroup is not null) &&
                (pageId is not null) &&
                (pageId != workgroup.ConfluencePageId))
            {
                workgroup.ConfluencePageId = pageId;
                workgroup.Update(_db);
            }
            else if ((workgroup is null) &&
                     (pageId is not null) &&
                     (pageId != _wgNotSpecifiedPageId))
            {
                _wgNotSpecifiedPageId = pageId;
            }
        }
    }


    private void writeToFile(string filename, string content, string? dir = null)
    {
        dir ??= _config.LocalExportDir;

        if (dir is not null)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string fullFilename = Path.Combine(dir, filename);
            File.WriteAllText(
                fullFilename,
                "<html><head><style>table, th, td {border: 1px solid;}</style></head><body>\n" + content + "\n</body></html>");
        }
    }

    private class ConfluenceSpace
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
    }

    private class ConfluenceStorage
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("representation")]
        public string Representation { get; set; } = "storage";

    }

    private class ConfluenceBody
    {
        [JsonPropertyName("storage")]
        public ConfluenceStorage Storage { get; set; } = new();
    }

    private class ConfluenceAncestor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }


    private class ConfluencePage
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; } = null;

        [JsonPropertyName("type")]
        public string ConfluenceType { get; set; } = "page";

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public ConfluenceBody Body { get; set; } = new();

        [JsonPropertyName("space")]
        public ConfluenceSpace Space { get; set; } = new();

        [JsonPropertyName("ancestors")]
        public List<ConfluenceAncestor> Ancestors { get; set; } = [];

        [JsonPropertyName("version")]
        public string? Version { get; set; } = null;
    }

    private int? writeToConfluence(int parentPageId, int? pageId, string title, string content)
    {
        if (_httpClient is null)
        {
            return null;
        }

        // if we have no page id, try to find the page by title under the parent
        if (pageId is null)
        {
            try
            {
                Uri findUri = new Uri(new Uri(_config.ConfluenceBaseUrl!), "rest/api/content");
                string query = $$$"""{ "space": { "key": "{{{_config.ConfluenceSpaceKey}}}" }, "title": "{{{title}}}" }""";

                HttpRequestMessage findRequest = new(HttpMethod.Get, findUri);
                findRequest.Content = new StringContent(query, Encoding.UTF8, "application/json");
                HttpResponseMessage findResponse = _httpClient.Send(findRequest);

                if (findResponse.IsSuccessStatusCode)
                {
                    string findResponseJson = findResponse.Content.ReadAsStringAsync().Result;
                    ConfluencePage? existingPage = System.Text.Json.JsonSerializer.Deserialize<ConfluencePage>(findResponseJson);
                    if ((existingPage is not null) &&
                        (existingPage.Id is not null))
                    {
                        pageId = int.Parse(existingPage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding page '{title}': {ex.Message}");
            }
        }

        try
        {
            // create our page object to serialize
            ConfluencePage page = new()
            {
                Id = pageId?.ToString(),
                Title = title,
                Body = new()
                {
                    Storage = new()
                    {
                        Value = content
                    },
                },
                Space = new()
                {
                    Key = _config.ConfluenceSpaceKey!
                },
                Ancestors = [new() { Id = parentPageId.ToString() }],
            };

            string json = System.Text.Json.JsonSerializer.Serialize(page);

            Uri requestUri = pageId is null
                ? new Uri(new Uri(_config.ConfluenceBaseUrl!), "rest/api/content")
                : new Uri(new Uri(_config.ConfluenceBaseUrl!), $"rest/api/content/{pageId}");

            HttpRequestMessage request = pageId is null
                ? new HttpRequestMessage(HttpMethod.Post, requestUri)
                : new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = _httpClient.Send(request);
            if (response.IsSuccessStatusCode)
            {
                string responseJson = response.Content.ReadAsStringAsync().Result;
                ConfluencePage? newPage = System.Text.Json.JsonSerializer.Deserialize<ConfluencePage>(responseJson);
                if ((newPage is not null) &&
                    (newPage.Id is not null))
                {
                    return int.Parse(newPage.Id);
                }
            }
            else
            {
                Console.WriteLine($"Error writing page '{title}': {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing page '{title}': {ex.Message}");
        }

        return null;
    }
}

