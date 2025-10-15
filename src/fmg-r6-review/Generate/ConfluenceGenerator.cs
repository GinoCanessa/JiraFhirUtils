using JiraFhirUtils.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

    public void Generate()
    {
        using IDbConnection db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DbPath}");
        db.Open();
        _db = db;

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

        // clean up
        _db.Close();
        _db = null!;
        _ciDb.Close();
        _ciDb = null!;
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

    private void genSpecPageContent(SpecPageRecord page)
    {
        string wgCode = page.ResponsibleWorkGroup ?? _missingWgCode;
        string pageName = Path.GetFileNameWithoutExtension(page.PageFileName);

        // resolve related lists
        List<SpecPageRemovedFhirArtifactRecord> removedFhirArtifactRecords = SpecPageRemovedFhirArtifactRecord.SelectList(_db, PageId: page.Id);
        List<SpecPageUnknownWordRecord> unknownWordRecords = SpecPageUnknownWordRecord.SelectList(_db, PageId: page.Id);
        List<SpecPageImageRecord> imgIssueRecords = SpecPageImageRecord.SelectList(_db, PageId: page.Id);

        string content = $$$"""
            <ac:structured-macro ac:name="panel">
                <ac:parameter ac:name="title">R6 Checklist for {{{page.PageFileName}}}</ac:parameter>
                <ac:rich-text-body>
                    <h2>Specification Page: {{{page.PageFileName}}}</h2>

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
                                <td><code>deprecated</code> Literal Count</td>
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
                </ac:rich-text-body>
            </ac:structured-macro>
            """;

        if (_config.LocalExportDir is not null)
        {
            writeToFile($"{pageName}.html", content, Path.Combine(_config.LocalExportDir, wgCode));
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
        List<WorkgroupRecord> workgroups = WorkgroupRecord.SelectList(_db);

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
            ? SpecPageRecord.SelectList(_db, ResponsibleWorkGroupIsNull: true, ArtifactIdIsNull: true)
            : SpecPageRecord.SelectList(_db, ResponsibleWorkGroup: workgroup.Code, ArtifactIdIsNull: true);

        // build the list of artifacts for this workgroup
        List<ArtifactRecord> artifacts = workgroup is null
            ? ArtifactRecord.SelectList(_db, ResponsibleWorkGroupIsNull: true)
            : ArtifactRecord.SelectList(_db, ResponsibleWorkGroup: workgroup.Code);

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
            writeToFile($"{wgCode}-root-page.html", content);
            string wgDir = Path.Combine(_config.LocalExportDir, wgCode);
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
            File.WriteAllText(fullFilename, "<html><body>\n" + content + "\n</body></html>");
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

