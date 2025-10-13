using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using JiraFhirUtils.Common;
using JiraFhirUtils.Common.FhirDbModels;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace fmg_r6_review.SpecReview;

public class PageReview
{
    private CliConfig _config;

    private static readonly char[] _wordSplitChars = [' ', '\t', '\r', '\n', ':', '\\', '/', '"', ',' ];

    private bool _haveDictDb = false;
    private FrozenSet<string> _dictionaryWords;
    private FrozenDictionary<string, string?> _typoWords;

    private bool _haveCiDb = false;
    private FrozenDictionary<string, string> _fhirCiStructures;
    private FrozenSet<string> _fhirCiElementPaths;
    private FrozenSet<string> _fhirCiValueSetNames;
    private FrozenSet<string> _fhirCiCodeSystemNames;
    private FrozenSet<string> _fhirCiOperationNames;
    private FrozenSet<string> _fhirCiSearchParameterNames;

    private bool _havePublishedDb = false;
    private FrozenDictionary<string, string> _fhirPublishedStructures;
    private FrozenSet<string> _fhirPublishedElementPaths;
    private FrozenSet<string> _fhirPublishedValueSetNames;
    private FrozenSet<string> _fhirPublishedCodeSystemNames;
    private FrozenSet<string> _fhirPublishedOperationNames;
    private FrozenSet<string> _fhirPublishedSearchParameterNames;

    private static readonly Regex _incompleteMarkerRegex = new(
        @"\b(to-do|todo|to\s+do|will\s+consider|...|future\s+versions)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _readerReviewRegex = new(
        @"\b(" +
        @"\[%stu-note%\]|stu-note|stu\s+note" +
        @"|\[%impl-note%\]|implementation\s+note|implementer\s+note|note\s+to\s+implementers" +
        @"|\[%feedback-note%\]|feedback" +
        @"|\[%dragons-start%\]|dragon" +
        @"|balloters|voters" +
        @")\b", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _trialUseTagRegex = new(
        @"(>\s*Trial\s+Use\s*<|class\s*=\s*['""]stu['""])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _zulipLinkRegex = new(
        @"http[s?]://chat.hl7.org/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _confluenceLinkRegex = new(
        @"http[s?]://confluence.hl7.org/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _htmlStripRegex = new("<.*?>", RegexOptions.Compiled);

    private IHtmlParser _htmlParser = new HtmlParser();

    private enum ConformanceKeywordCodes
    {
        Shall,
        ShallNot,
        Should,
        ShouldNot,
        May,
        MayNot,
    }

    private static readonly Regex _conformantShallRegex = new(@"\bSHALL\b(?!\s+NOT)", RegexOptions.Compiled);
    private static readonly Regex _totalShallRegex = new(@"\bSHALL\b(?!\s+NOT)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _conformantShallNotRegex = new(@"\bSHALL\s+NOT\b", RegexOptions.Compiled);
    private static readonly Regex _totalShallNotRegex = new(@"\bSHALL\s+NOT\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _conformantShouldRegex = new(@"\bSHOULD\b(?!\s+NOT)", RegexOptions.Compiled);
    private static readonly Regex _totalShouldRegex = new(@"\bSHOULD\b(?!\s+NOT)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _conformantShouldNotRegex = new(@"\bSHOULD\s+NOT\b", RegexOptions.Compiled);
    private static readonly Regex _totalShouldNotRegex = new(@"\bSHOULD\s+NOT\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _conformantMayRegex = new(@"\bMAY\b(?!\s+NOT)", RegexOptions.Compiled);
    private static readonly Regex _totalMayRegex = new(@"\bMAY\b(?!\s+NOT)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _conformantMayNotRegex = new(@"\bMAY\s+NOT\b", RegexOptions.Compiled);
    private static readonly Regex _totalMayNotRegex = new(@"\bMAY\s+NOT\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);



    private static readonly HashSet<string> _priorFhirVersionKeywords = new([
        "DSTU1", "DSTU2", "STU3", "R4", "R4B", "R5",
        "R2", "R3",
        ], StringComparer.OrdinalIgnoreCase);

    private string _fhirRepoPath;
    private IDbConnection? _db = null;

    public PageReview(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(_config.DbPath))
        {
            throw new ArgumentException("DbPath must be provided.");
        }

        if (string.IsNullOrEmpty(_config.FhirRepoPath))
        {
            throw new ArgumentException("FhirRepoPath must be provided.");
        }

        _fhirRepoPath = Path.GetFullPath(_config.FhirRepoPath);

        if (!string.IsNullOrEmpty(_config.DictionaryDbPath) &&
            File.Exists(_config.DictionaryDbPath))
        {
            using IDbConnection dictDb = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DictionaryDbPath}");
            dictDb.Open();

            _dictionaryWords = loadDictionaryWords(dictDb);
            _typoWords = loadTypoWords(dictDb);

            _haveDictDb = true;
        }
        else
        {
            _dictionaryWords = Array.Empty<string>().ToFrozenSet();
            _typoWords = new Dictionary<string, string?>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(_config.FhirDatabaseCi) &&
            File.Exists(_config.FhirDatabaseCi))
        {
            using IDbConnection ciDb = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.FhirDatabaseCi}");
            ciDb.Open();

            _fhirCiStructures = loadFhirStructures(ciDb);
            _fhirCiElementPaths = loadFhirElementPaths(ciDb);
            _fhirCiValueSetNames = loadFhirValueSetNames(ciDb);
            _fhirCiCodeSystemNames = loadFhirCodeSystemNames(ciDb);
            _fhirCiOperationNames = loadFhirOperationNames(ciDb);
            _fhirCiSearchParameterNames = loadFhirSearchParameterNames(ciDb);

            _haveCiDb = true;
        }
        else
        {
            _fhirCiStructures = new Dictionary<string, string>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            _fhirCiElementPaths = Array.Empty<string>().ToFrozenSet();
            _fhirCiValueSetNames = Array.Empty<string>().ToFrozenSet();
            _fhirCiCodeSystemNames = Array.Empty<string>().ToFrozenSet();
            _fhirCiOperationNames = Array.Empty<string>().ToFrozenSet();
            _fhirCiSearchParameterNames = Array.Empty<string>().ToFrozenSet();
        }

        if (!string.IsNullOrEmpty(_config.FhirDatabasePublished) &&
            File.Exists(_config.FhirDatabasePublished))
        {
            using IDbConnection fhirDb = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.FhirDatabasePublished}");
            fhirDb.Open();
            _fhirPublishedStructures = loadFhirStructures(fhirDb);
            _fhirPublishedElementPaths = loadFhirElementPaths(fhirDb);
            _fhirPublishedValueSetNames = loadFhirValueSetNames(fhirDb);
            _fhirPublishedCodeSystemNames = loadFhirCodeSystemNames(fhirDb);
            _fhirPublishedOperationNames = loadFhirOperationNames(fhirDb);
            _fhirPublishedSearchParameterNames = loadFhirSearchParameterNames(fhirDb);

            _havePublishedDb = true;
        }
        else
        {
            _fhirPublishedStructures = new Dictionary<string, string>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            _fhirPublishedElementPaths = Array.Empty<string>().ToFrozenSet();
            _fhirPublishedValueSetNames = Array.Empty<string>().ToFrozenSet();
            _fhirPublishedCodeSystemNames = Array.Empty<string>().ToFrozenSet();
            _fhirPublishedOperationNames = Array.Empty<string>().ToFrozenSet();
            _fhirPublishedSearchParameterNames = Array.Empty<string>().ToFrozenSet();
        }
    }

    private FrozenSet<string> loadFhirSearchParameterNames(IDbConnection db)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbSearchParameter.Name)}" +
            $" FROM {CgDbSearchParameter.DefaultTableName}" +
            $" WHERE {nameof(CgDbSearchParameter.Name)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            names.Add(name);
        }
        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadFhirOperationNames(IDbConnection db)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbOperation.Name)}" +
            $" FROM {CgDbOperation.DefaultTableName}" +
            $" WHERE {nameof(CgDbOperation.Name)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            names.Add(name);
        }
        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadFhirCodeSystemNames(IDbConnection db)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbCodeSystem.Name)}" +
            $" FROM {CgDbCodeSystem.DefaultTableName}" +
            $" WHERE {nameof(CgDbCodeSystem.Name)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            names.Add(name);
        }
        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadFhirValueSetNames(IDbConnection db)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbValueSet.Name)}" +
            $" FROM {CgDbValueSet.DefaultTableName}" +
            $" WHERE {nameof(CgDbValueSet.Name)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            names.Add(name);
        }
        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadFhirElementPaths(IDbConnection db)
    {
        HashSet<string> paths = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbElement.Path)}" +
            $" FROM {CgDbElement.DefaultTableName}" +
            $" WHERE {nameof(CgDbElement.Path)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string path = reader.GetString(0);
            paths.Add(path);
        }
        return paths.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenDictionary<string, string> loadFhirStructures(IDbConnection db)
    {
        Dictionary<string, string> structures = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbStructure.Name)}, {nameof(CgDbStructure.ArtifactClass)}" +
            $" FROM {CgDbStructure.DefaultTableName}" +
            $" WHERE {nameof(CgDbStructure.Name)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            string artifactClass = reader.GetString(1);
            structures[name] = artifactClass;
        }
        return structures.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenDictionary<string, string?> loadTypoWords(IDbConnection db)
    {
        Dictionary<string, string?> typos = new(StringComparer.Ordinal);
        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(DictTypoRecord.Typo)}, {nameof(DictTypoRecord.Correction)}" +
            $" FROM {DictTypoRecord.DefaultTableName}" +
            $" WHERE {nameof(DictTypoRecord.Typo)} IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            (string typo, bool hasAlpha) = sanitizeAsKeyword(reader.GetString(0));
            if (string.IsNullOrEmpty(typo))
            {
                continue;
            }

            string? suggestion = reader.GetString(1);
            typos[typo] = suggestion;
        }
        return typos.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadDictionaryWords(IDbConnection db)
    {
        HashSet<string> words = new(StringComparer.Ordinal);

        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(DictWordRecord.Word)} FROM {DictWordRecord.DefaultTableName} WHERE word IS NOT NULL;";

        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            (string word, bool hasAlpha) = sanitizeAsKeyword(reader.GetString(0));
            if (hasAlpha)
            {
                words.Add(word);
            }
        }

        return words.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public void ProcessPages()
    {
        Console.WriteLine("Processing specification pages...");
        Console.WriteLine($"Using database: {_config.DbPath}");
        Console.WriteLine($"Using FHIR repository path: {_config.FhirRepoPath}");

        using IDbConnection db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_config.DbPath}");
        db.Open();

        _db = db;

        // ensure our tables exist
        if (_config.DropTables)
        {
            SpecPageRecord.DropTable(_db);
            SpecPageImageRecord.DropTable(_db);
            SpecPageRemovedFhirArtifactRecord.DropTable(_db);
            SpecPageUnknownWordRecord.DropTable(_db);
        }

        SpecPageRecord.CreateTable(_db);
        SpecPageImageRecord.CreateTable(_db);
        SpecPageRemovedFhirArtifactRecord.CreateTable(_db);
        SpecPageUnknownWordRecord.CreateTable(_db);

        // load the initial set of pages we are working with
        List<SpecPageRecord> pages = buildSpecPageList();

        // traverse the pages and perform review checks
        foreach (SpecPageRecord page in pages)
        {
            Console.WriteLine($"Processing page '{page.PageFileName}'...");
            doPageReview(page);
        }
    }

    private void doPageReview(SpecPageRecord page)
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }

        if (!page.ExistsInSource)
        {
            // nothing to do if the source file doesn't exist
            return;
        }

        SpecPageRecord modified = page with { };

        string sourceFilePath = Path.Combine(_fhirRepoPath, "source", page.PageFileName);
        if (!File.Exists(sourceFilePath))
        {
            Console.WriteLine($"Source file not found for page {page.PageFileName}");
            modified.ExistsInSource = false;
            modified.Update(_db);
            return;
        }

        try
        {
            // read the source file
            string htmlContent = File.ReadAllText(sourceFilePath);

            // Parse HTML
            IDocument? doc = null;
            try
            {
                doc = _htmlParser.ParseDocument(htmlContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing HTML for page {page.PageFileName}: {ex.Message}");
                if (doc is null)
                {
                    throw;
                }
            }

            string visibleText = extractVisibleText(doc);

            bool isStandardPage = true;

            // perform necessary checks
            modified.ResponsibleWorkGroup = extractWorkGroup(doc);

            (isStandardPage, modified.MaturityLabel, modified.MaturityLevel, modified.StandardsStatus) = extractStatusInfo(doc);

            if (!isStandardPage)
            {
                return;
            }

            (modified.ConformantShallCount, modified.NonConformantShallCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.Shall);
            (modified.ConformantShallNotCount, modified.NonConformantShallNotCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.ShallNot);
            (modified.ConformantShouldCount, modified.NonConformantShouldCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.Should);
            (modified.ConformantShouldNotCount, modified.NonConformantShouldNotCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.ShouldNot);
            (modified.ConformantMayCount, modified.NonConformantMayCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.May);
            (modified.ConformantMayNotCount, modified.NonConformantMayNotCount) = getConformanceCounts(visibleText, ConformanceKeywordCodes.MayNot);

            modified.ConformantTotalCount = (modified.ConformantShallCount ?? 0) +
                                            (modified.ConformantShallNotCount ?? 0) +
                                            (modified.ConformantShouldCount ?? 0) +
                                            (modified.ConformantShouldNotCount ?? 0) +
                                            (modified.ConformantMayCount ?? 0) +
                                            (modified.ConformantMayNotCount ?? 0);

            modified.NonConformantTotalCount = (modified.NonConformantShallCount ?? 0) +
                                               (modified.NonConformantShallNotCount ?? 0) +
                                               (modified.NonConformantShouldCount ?? 0) +
                                               (modified.NonConformantShouldNotCount ?? 0) +
                                               (modified.NonConformantMayCount ?? 0) +
                                               (modified.NonConformantMayNotCount ?? 0);

            // process the visible text into words
            string[] words = visibleText
                .Split(_wordSplitChars, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();

            int priorFhirVersionCount = 0;
            int deprecatedLiteralCount = 0;

            foreach (string rawWord in words)
            {
                string word = rawWord.Trim();

                if (word.StartsWith("[%", StringComparison.Ordinal) || 
                    word.EndsWith("%]", StringComparison.Ordinal))
                {
                    // skip special markup words
                    continue;
                }

                (string sanitized, bool hasAlpha) = sanitizeAsKeyword(word);

                bool inCi = false;
                string? ciArtifactClass = null;

                bool inPublished = false;
                string? publishedArtifactClass = null;

                // check if this word is a FHIR artifact in the current ci
                if (_haveCiDb)
                {
                    (inCi, ciArtifactClass) = checkFhirCi(word, sanitized);
                }

                // check if this word is a FHIR artifact in the published spec
                if (_havePublishedDb)
                {
                    (inPublished, publishedArtifactClass) = checkFhirPublished(word, sanitized);
                }

                // check if this is a 'FHIR' term that has been removed
                if (inPublished && !inCi)
                {
                    // check to see if we have an existing record for this word
                    SpecPageRemovedFhirArtifactRecord? removedWord = SpecPageRemovedFhirArtifactRecord.SelectSingle(
                        _db,
                        PageId: page.PageId,
                        Word: word);

                    if (removedWord is null)
                    {
                        // new record
                        removedWord = new SpecPageRemovedFhirArtifactRecord
                        {
                            Id = SpecPageRemovedFhirArtifactRecord.GetIndex(),
                            PageId = page.PageId,
                            Word = word,
                            ArtifactClass = publishedArtifactClass ?? "Unknown",
                        };

                        removedWord.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
                    }
                }

                // check if this is an unknown word
                if (!inCi &&
                    !inPublished &&
                    _haveDictDb)
                {
                    // check typo list first
                    if (_typoWords.ContainsKey(sanitized))
                    {
                        // check to see if we have an existing record for this word
                        SpecPageUnknownWordRecord? typoRecord = SpecPageUnknownWordRecord.SelectSingle(
                            _db,
                            PageId: page.PageId,
                            Word: word);

                        if (typoRecord is null)
                        {
                            // new record
                            typoRecord = new SpecPageUnknownWordRecord
                            {
                                Id = SpecPageUnknownWordRecord.GetIndex(),
                                PageId = page.PageId,
                                Word = word,
                                IsTypo = true,
                            };
                            typoRecord.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
                        }
                    }
                    // check word list next
                    else if (_dictionaryWords.Contains(sanitized))
                    {
                        // known word, nothing to do
                    }
                    else if (hasAlpha)
                    {
                        // check to see if we have an existing record for this word
                        SpecPageUnknownWordRecord? unknownWord = SpecPageUnknownWordRecord.SelectSingle(
                            _db,
                            PageId: page.PageId,
                            Word: word);
                        if (unknownWord is null)
                        {
                            // new record
                            unknownWord = new SpecPageUnknownWordRecord
                            {
                                Id = SpecPageUnknownWordRecord.GetIndex(),
                                PageId = page.PageId,
                                Word = word,
                                IsTypo = false,
                            };
                            unknownWord.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
                        }
                    }
                }

                // check to see if this word is a prior FHIR version keyword
                if (_priorFhirVersionKeywords.Contains(sanitized, StringComparer.OrdinalIgnoreCase))
                {
                    priorFhirVersionCount++;
                }

                // check to see if this is the word 'deprecated'
                if (string.Equals(sanitized, "deprecated", StringComparison.Ordinal))
                {
                    deprecatedLiteralCount++;
                }
            }

            // update word-based totals
            modified.RemovedFhirArtifactCount = SpecPageRemovedFhirArtifactRecord.SelectCount(_db, PageId: page.PageId);
            modified.UnknownWordCount = SpecPageUnknownWordRecord.SelectCount(_db, PageId: page.PageId, IsTypo: false);
            modified.TypoWordCount = SpecPageUnknownWordRecord.SelectCount(_db, PageId: page.PageId, IsTypo: true);
            modified.PriorFhirVersionReferenceCount = priorFhirVersionCount;
            modified.DeprecatedLiteralCount = deprecatedLiteralCount;

            // perform image checks
            modified.ImagesWithIssuesCount = checkPageImages(doc, page.PageId);

            // check for incomplete markers
            modified.PossibleIncompleteMarkers = _incompleteMarkerRegex.Matches(visibleText)
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // check for reader review notes
            modified.ReaderReviewNotes = _readerReviewRegex.Matches(visibleText)
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // check for trial use tags
            modified.StuLiteralsCount = _trialUseTagRegex.Matches(htmlContent).Count;

            // check for links
            modified.ZulipLinkCount = _zulipLinkRegex.Matches(visibleText).Count;
            modified.ConfluenceLinkCount = _confluenceLinkRegex.Matches(visibleText).Count;

            // update our record if necessary
            if (modified != page)
            {
                modified.Update(_db);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing page {page.PageFileName}: {ex.Message}");
        }
    }

    private int checkPageImages(IDocument doc, int pageId)
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }

        int imagesWithIssuesCount = 0;

        // iterate over all the 'img' tags in the document
        foreach (IElement img in doc.QuerySelectorAll("img"))
        {
            string? src = img.GetAttribute("src");

            if (src is null)
            {
                // skip images with no src attribute
                continue;
            }

            bool missingAlt = false;
            bool notInFigure = false;

            // check for alt attribute
            if (!img.HasAttribute("alt") || 
                string.IsNullOrWhiteSpace(img.GetAttribute("alt")))
            {
                missingAlt = true;
            }

            // check to see if the parent is a 'figure' tag
            if (img.ParentElement is null || 
                !string.Equals(img.ParentElement.TagName, "figure", StringComparison.OrdinalIgnoreCase))
            {
                notInFigure = true;
            }

            if (missingAlt || notInFigure)
            {
                imagesWithIssuesCount++;

                // check to see if we already have an image record
                SpecPageImageRecord? imageRecord = SpecPageImageRecord.SelectSingle(
                    _db,
                    PageId: pageId,
                    Source: src);

                if (imageRecord is null)
                {
                    imageRecord = new SpecPageImageRecord
                    {
                        Id = SpecPageImageRecord.GetIndex(),
                        PageId = pageId,
                        Source = src,
                        MissingAlt = missingAlt,
                        NotInFigure = notInFigure,
                    };
                    imageRecord.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
                }
                else
                {
                    // update existing record if necessary
                    if (imageRecord.MissingAlt != missingAlt ||
                        imageRecord.NotInFigure != notInFigure)
                    {
                        imageRecord.MissingAlt = missingAlt;
                        imageRecord.NotInFigure = notInFigure;
                        imageRecord.Update(_db);
                    }
                }
            }
        }

        return imagesWithIssuesCount;
    }

    private (bool found, string? artifactClass) checkFhirCi(string word, string sanitized)
    {
        string trimmedWord = word.Trim();
        if (string.IsNullOrEmpty(trimmedWord))
        {
            return (false, null);
        }

        // check if this is supposed to be an operation
        if (trimmedWord.StartsWith('$'))
        {
            trimmedWord = trimmedWord[1..];
            if (_fhirCiOperationNames.Contains(trimmedWord))
            {
                return (true, "Operation");
            }

            if (_fhirCiOperationNames.Contains(sanitized))
            {
                return (true, "Operation");
            }

            return (false, null);
        }

        // check if there is a structure definition for this 'word'
        if (_fhirCiStructures.TryGetValue(trimmedWord, out string? artifactClass))
        {
            return (true, artifactClass);
        }

        if (_fhirCiStructures.TryGetValue(sanitized, out artifactClass))
        {
            return (true, artifactClass);
        }

        // check if there is an element for this 'word'
        if (_fhirCiElementPaths.Contains(trimmedWord))
        {
            return (true, "Element");
        }

        if (_fhirCiElementPaths.Contains(sanitized))
        {
            return (true, "Element");
        }

        // check if there is a value set for this 'word'
        if (_fhirCiValueSetNames.Contains(trimmedWord))
        {
            return (true, "ValueSet");
        }

        if (_fhirCiValueSetNames.Contains(sanitized))
        {
            return (true, "ValueSet");
        }

        // check if there is a code system for this 'word'
        if (_fhirCiCodeSystemNames.Contains(trimmedWord))
        {
            return (true, "CodeSystem");
        }

        if (_fhirCiCodeSystemNames.Contains(sanitized))
        {
            return (true, "CodeSystem");
        }

        // check if there is a search parameter for this 'word'
        if (_fhirCiSearchParameterNames.Contains(trimmedWord))
        {
            return (true, "SearchParameter");
        }

        if (_fhirCiSearchParameterNames.Contains(sanitized))
        {
            return (true, "SearchParameter");
        }

        return (false, null);
    }

    private (bool found, string? artifactClass) checkFhirPublished(string word, string sanitized)
    {
        string trimmedWord = word.Trim();
        if (string.IsNullOrEmpty(trimmedWord))
        {
            return (false, null);
        }

        // check if this is supposed to be an operation
        if (trimmedWord.StartsWith('$'))
        {
            trimmedWord = trimmedWord[1..];
            if (_fhirPublishedOperationNames.Contains(trimmedWord))
            {
                return (true, "Operation");
            }

            if (_fhirPublishedOperationNames.Contains(sanitized))
            {
                return (true, "Operation");
            }

            return (false, null);
        }

        // check if there is a structure definition for this 'word'
        if (_fhirPublishedStructures.TryGetValue(trimmedWord, out string? artifactClass))
        {
            return (true, artifactClass);
        }

        if (_fhirPublishedStructures.TryGetValue(sanitized, out artifactClass))
        {
            return (true, artifactClass);
        }

        // check if there is an element for this 'word'
        if (_fhirPublishedElementPaths.Contains(trimmedWord))
        {
            return (true, "Element");
        }

        if (_fhirPublishedElementPaths.Contains(sanitized))
        {
            return (true, "Element");
        }

        // check if there is a value set for this 'word'
        if (_fhirPublishedValueSetNames.Contains(trimmedWord))
        {
            return (true, "ValueSet");
        }

        if (_fhirPublishedValueSetNames.Contains(sanitized))
        {
            return (true, "ValueSet");
        }

        // check if there is a code system for this 'word'
        if (_fhirPublishedCodeSystemNames.Contains(trimmedWord))
        {
            return (true, "CodeSystem");
        }

        if (_fhirPublishedCodeSystemNames.Contains(sanitized))
        {
            return (true, "CodeSystem");
        }

        // check if there is a search parameter for this 'word'
        if (_fhirPublishedSearchParameterNames.Contains(trimmedWord))
        {
            return (true, "SearchParameter");
        }

        if (_fhirPublishedSearchParameterNames.Contains(sanitized))
        {
            return (true, "SearchParameter");
        }

        return (false, null);
    }


    private (int conformant, int nonConformant) getConformanceCounts(string text, ConformanceKeywordCodes keywordCode)
    {
        int conformant = 0;
        int total = 0;

        switch (keywordCode)
        {
            case ConformanceKeywordCodes.Shall:
                conformant = _conformantShallRegex.Matches(text).Count;
                total = _totalShallRegex.Matches(text).Count;
                break;
            case ConformanceKeywordCodes.ShallNot:
                conformant = _conformantShallNotRegex.Matches(text).Count;
                total = _totalShallNotRegex.Matches(text).Count;
                break;
            case ConformanceKeywordCodes.Should:
                conformant = _conformantShouldRegex.Matches(text).Count;
                total = _totalShouldRegex.Matches(text).Count;
                break;
            case ConformanceKeywordCodes.ShouldNot:
                conformant = _conformantShouldNotRegex.Matches(text).Count;
                total = _totalShouldNotRegex.Matches(text).Count;
                break;
            case ConformanceKeywordCodes.May:
                conformant = _conformantMayRegex.Matches(text).Count;
                total = _totalMayRegex.Matches(text).Count;
                break;
            case ConformanceKeywordCodes.MayNot:
                conformant = _conformantMayNotRegex.Matches(text).Count;
                total = _totalMayNotRegex.Matches(text).Count;
                break;
            default:
                throw new Exception($"Unknown ConformanceKeywordCode: {keywordCode}");
        }

        return (conformant, total - conformant);
    }

    private (bool parseable, string? maturityLabel, int? maturityLevel, string? standardsStatus) extractStatusInfo(IDocument doc)
    {
        IElement? table = null;
        string? maturityLabel = null;
        int? maturityLevel = null;
        string? standardsStatus = null;

        if (doc.QuerySelector("table[class='colsd']") is IHtmlTableElement draftTable)
        {
            /// <example>
            /// <table class="colsd"><tr><td id="wg"><a _target="blank" href="[%wg fhir%]">[%wgt fhir%]</a> Work Group</td><td id="fmm"><a href="versions.html#maturity">Maturity Level</a>: 0</td><td id="ballot"><a href="versions.html#std-process">Standards Status</a>:<!--!ns!--><a href="versions.html#std-process">Draft</a></td></tr></table>
            /// </example>
            table = draftTable;
            maturityLabel = "Draft";
            standardsStatus = "Draft";
        }
        else if (doc.QuerySelector("table[class='colstu']") is IHtmlTableElement stuTable)
        {
            /// <example>
            /// <table class="colstu"><tr><td id="wg"><a _target="blank" href="[%wg vocab%]">[%wgt vocab%]</a> Work Group</td><td id="fmm"><a href="versions.html#maturity">Maturity Level</a>: 3</td><td id="ballot"><a href="versions.html#std-process">Standards Status</a>:<!--!ns!--><a href="versions.html#std-process">Trial Use</a></td></tr></table>
            /// </example>
            table = stuTable;
            maturityLabel = "STU";
            standardsStatus = "Trial Use";
        }
        else if (doc.QuerySelector("table[class='colsi']") is IHtmlTableElement informativeTable)
        {
            /// <example>
            /// <table class="colsi"><tr><td id="wg"><a _target="blank" href="[%wg vocab%]">[%wgt vocab%]</a> Work Group</td><td id="fmm"><a href="versions.html#maturity">Maturity Level</a>: N/A</td><td id="ballot"><a href="versions.html#std-process">Standards Status</a>:<!--!ns!--><a href="versions.html#std-process">Informative</a></td></tr></table>
            /// </example>
            table = informativeTable;
            maturityLabel = "Informative";
            standardsStatus = "Informative";
        }
        else if (doc.QuerySelector("table[class='colsn']") is IHtmlTableElement normativeTable)
        {
            /// <example>
            /// <table class="colsn"><tr><td id="wg"><a _target="blank" href="[%wg vocab%]">[%wgt vocab%]</a> Work Group</td><td id="fmm"><a href="versions.html#maturity">Maturity Level</a>: Normative</td><td id="ballot"><a href="versions.html#std-process">Standards Status</a>:<!--!ns!--><a href="versions.html#std-process">Normative</a></td></tr></table>
            /// </example>
            table = normativeTable;
            maturityLabel = "Normative";
            standardsStatus = "Normative";
        }
        else
        {
            IElement? firstTable = doc.QuerySelector("table");
            Console.WriteLine($"Unable to match header table class, first table class: '{firstTable?.ClassName}'!");
            return (false, null, null, null);
        }

        IElement? fmmCol = table.QuerySelector("td[id='fmm']");
        if (fmmCol is IHtmlTableCellElement fmmCell)
        {
            string? content = fmmCell.TextContent?.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                // Extract text after "Maturity Level:" or ":"
                int colonIndex = content.LastIndexOf(':');
                if (colonIndex >= 0 && colonIndex < content.Length - 1)
                {
                    string fmmText = content.Substring(colonIndex + 1).Trim();

                    // Try to parse as integer
                    if (int.TryParse(fmmText, out int level))
                    {
                        maturityLevel = level;
                    }
                }
            }
        }

        IElement? ballotCol = table.QuerySelector("td[id='ballot']");
        if (ballotCol is IHtmlTableCellElement ballotCell)
        {
            string? content = ballotCell.TextContent?.Trim();
            if (!string.IsNullOrEmpty(content))
            {
                int colonIndex = content.LastIndexOf(':');
                if (colonIndex >= 0)
                {
                    standardsStatus = content[(colonIndex + 1)..].Trim();
                }
                else
                {
                    standardsStatus = content.Trim();
                }
            }
        }

        return (true, maturityLabel, maturityLevel, standardsStatus);
    }

    private string? extractWorkGroup(IDocument doc)
    {
        IElement? wgElement = doc.QuerySelector("td[id='wg']");
        if (wgElement is IHtmlTableCellElement cellEl)
        {
            string? content = cellEl.TextContent?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            // format of text is `[%wg vocab%]`
            string[] parts = content.Split(new[] { ' ' }, 2);
            if ((parts.Length == 2) && 
                parts[0].StartsWith("[%wg ", StringComparison.Ordinal) && 
                parts[0].EndsWith("%]", StringComparison.Ordinal))
            {
                return parts[1];
            }
        }
        return null;
    }

    private static string extractVisibleText(IDocument doc)
    {
        StringBuilder sb = new();
        foreach (INode node in doc.Body?.Descendants() ?? Enumerable.Empty<INode>())
        {
            if (node is IElement el)
            {
                if (el.TagName is "SCRIPT" or "STYLE" or "NAV" or "HEADER" or "FOOTER")
                {
                    // skip subtree
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(node.TextContent))
            {
                string text = normalizeWhitespace(node.TextContent);
                if (text.Length > 0)
                {
                    sb.Append(text);
                    sb.Append('\n');
                }
            }
        }

        return stripHtml(sb.ToString());
    }

    private static string normalizeWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        Span<char> bufferSpan = stackalloc char[input.Length];
        int j = 0;
        bool inWs = false;
        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWs)
                {
                    bufferSpan[j++] = ' ';
                    inWs = true;
                }
            }
            else
            {
                bufferSpan[j++] = c;
                inWs = false;
            }
        }
        return new string(bufferSpan[..j]).Trim();
    }

    private List<SpecPageRecord> buildSpecPageList()
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }

        // load existing spec pages from the database
        List<SpecPageRecord> dbPages = SpecPageRecord.SelectList(_db);

        // load the list of pages from the FHIR repository
        string publishIniPath = Path.Combine(_fhirRepoPath, "publish.ini");

        HashSet<string> iniPages = getIniValues(publishIniPath, "pages")
            .Select(kvp => kvp.key)
            .ToHashSet();

        List<SpecPageRecord> toInsert = [];
        List<SpecPageRecord> toUpdate = [];

        foreach (SpecPageRecord page in dbPages)
        {
            bool needsUpdate = false;

            bool existsInPublishIni = iniPages.Contains(page.PageFileName);
            bool existsInSource = File.Exists(Path.Combine(_fhirRepoPath, "source", page.PageFileName));

            if (page.ExistsInPublishIni != existsInPublishIni)
            {
                page.ExistsInPublishIni = existsInPublishIni;
                needsUpdate = true;
            }

            if (page.ExistsInSource != existsInSource)
            {
                page.ExistsInSource = existsInSource;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                toUpdate.Add(page);
            }
        }

        ILookup<string, SpecPageRecord> dbPageLookup = dbPages.ToLookup(p => p.PageFileName, StringComparer.OrdinalIgnoreCase);
        foreach (string iniPage in iniPages)
        {
            if (!dbPageLookup.Contains(iniPage))
            {
                SpecPageRecord newPage = new()
                {
                    PageId = SpecPageRecord.GetIndex(),
                    PageFileName = iniPage,
                    ExistsInPublishIni = true,
                    ExistsInSource = File.Exists(Path.Combine(_fhirRepoPath, "source", iniPage)),
                };
                dbPages.Add(newPage);
                toInsert.Add(newPage);
            }
        }

        if (toInsert.Count > 0)
        {
            toInsert.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
        }

        if (toUpdate.Count > 0)
        {
            toUpdate.Update(_db);
        }

        return dbPages;
    }

    private List<(string key, string value)> getIniValues(string filename, string section)
    {
        List<(string key, string value)> iniValues = [];

        if (!File.Exists(filename))
        {
            throw new FileNotFoundException($"INI file not found: {filename}");
        }

        // find the section we care about
        string sectionMatch = $"[{section}]";

        bool inSection = false;
        foreach (string line in File.ReadLines(filename))
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith('[') && 
                trimmedLine.EndsWith(']'))
            {
                inSection = trimmedLine.Equals(sectionMatch, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inSection)
            {
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
                {
                    // skip empty lines and comments
                    continue;
                }
                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    iniValues.Add((key, value));
                }
            }
        }

        return iniValues;
    }

    private static string stripHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        // simple regex to remove HTML tags
        return _htmlStripRegex.Replace(text, string.Empty);
    }

    private static (string clean, bool hasAlpha) sanitizeAsKeyword(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (string.Empty, false);
        }

        bool hasAlpha = false;

        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);

            switch (uc)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    sb.Append(char.ToLower(c));
                    hasAlpha = true;
                    break;

                case UnicodeCategory.LowercaseLetter:
                    hasAlpha = true;
                    sb.Append(c);
                    break;

                case UnicodeCategory.DecimalDigitNumber:
                    sb.Append(c);
                    break;

                //case UnicodeCategory.ModifierLetter:
                //case UnicodeCategory.OtherLetter:
                //case UnicodeCategory.NonSpacingMark:
                //case UnicodeCategory.SpacingCombiningMark:
                //case UnicodeCategory.EnclosingMark:
                //case UnicodeCategory.LetterNumber:
                //case UnicodeCategory.OtherNumber:
                //case UnicodeCategory.SpaceSeparator:
                //case UnicodeCategory.LineSeparator:
                //case UnicodeCategory.ParagraphSeparator:
                //case UnicodeCategory.Control:
                //case UnicodeCategory.Format:
                //case UnicodeCategory.Surrogate:
                //case UnicodeCategory.PrivateUse:
                //case UnicodeCategory.ConnectorPunctuation:
                //case UnicodeCategory.DashPunctuation:
                //case UnicodeCategory.OpenPunctuation:
                //case UnicodeCategory.ClosePunctuation:
                //case UnicodeCategory.InitialQuotePunctuation:
                //case UnicodeCategory.FinalQuotePunctuation:
                //case UnicodeCategory.OtherPunctuation:
                //case UnicodeCategory.MathSymbol:
                //case UnicodeCategory.CurrencySymbol:
                //case UnicodeCategory.ModifierSymbol:
                //case UnicodeCategory.OtherSymbol:
                //case UnicodeCategory.OtherNotAssigned:
                default:
                    break;
            }
        }
        return (sb.ToString(), hasAlpha);
    }

}
