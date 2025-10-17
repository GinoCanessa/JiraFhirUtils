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

public class ContentReview
{
    private CliConfig _config;

    private static readonly char[] _wordSplitChars = [ ' ', '\t', '\r', '\n', '"' ];

    private static readonly char[] _extendedSplitChars = [
        ' ', '\t', '\r', '\n',
        '.',
        ':', '\\', '/', 
        '"', '\'', ';',
        '+', '-', '_', '#', '*', '&', '^', '%', '@', '!',
        ';', ',', '|', '?', '=',
        '{', '}', '(', ')', '[', ']',
        ];

    private static readonly char[] _wgSplitChars = [ ' ', '[', ']', '%' ];

    private bool _haveDictDb = false;
    private FrozenSet<string> _dictionaryWords;
    private FrozenDictionary<string, string?> _typoWords;

    private enum FhirSourceCodes : int
    {
        Ci,
        Published,
    }

    private bool _haveCiDb = false;
    private FrozenDictionary<string, string> _fhirCiStructures;
    private FrozenSet<string> _fhirCiElementPaths;
    private FrozenSet<string> _fhirCiValueSetNames;
    private FrozenSet<string> _fhirCiCodeSystemNames;
    private FrozenSet<string> _fhirCiCodes;
    private FrozenSet<string> _fhirCiOperationNames;
    private FrozenSet<string> _fhirCiSearchParameterNames;

    private bool _havePublishedDb = false;
    private FrozenDictionary<string, string> _fhirPublishedStructures;
    private FrozenSet<string> _fhirPublishedElementPaths;
    private FrozenSet<string> _fhirPublishedValueSetNames;
    private FrozenSet<string> _fhirPublishedCodeSystemNames;
    private FrozenSet<string> _fhirPublishedCodes;
    private FrozenSet<string> _fhirPublishedOperationNames;
    private FrozenSet<string> _fhirPublishedSearchParameterNames;

    private static readonly Regex _incompleteMarkerRegex = new(
        @"\b(to-do|todo|to\s+do|will\s+consider|\.\.\.|future\s+versions)\b",
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
        @"http[s?]:\/\/chat.fhir.org/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _confluenceLinkRegex = new(
        @"http[s?]://confluence.hl7.org/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _thoCodeSystemRegex = new(
        @"(http:\/\/|https:\/\/)?terminology.hl7.org(\/CodeSystem[^\s]*|\/temporary/CodeSystem[^\s]*)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _htmlStripRegex = new("<.*?>", RegexOptions.Compiled);

    private static readonly Regex _urlRegex = new(
        @"(http|https|ftp|sftp):\/\/[^\s\/$.?#].[^\s]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _baseFhirRegex = new(
        @"\[base\]\/[^\s]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _typeFhirRegex = new(
        @"\[type\]\/(\[type\]\/?)?[^\s]*",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _urnRegex = new(
        @"urn:[a-zA-Z0-9][a-zA-Z0-9-]{1,31}:[^\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _xsdRegex = new(
        @"xs[d]?:[a-zA-Z0-9._%+-]+(\/[a-zA-Z0-9._%+-]+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _fhirShexRegex = new(
        @"fhir:[a-zA-Z0-9._%+-]+(\/[a-zA-Z0-9._%+-]+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _emailAddressRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _dateTimeRegex = new(
        //@"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(Z|[+-]\d{2}:\d{2})\b",
        @"([0-9]([0-9]([0-9][1-9]|[1-9]0)|[1-9]00)|[1-9]000)(-(0[1-9]|1[0-2])(-(0[1-9]|[1-2][0-9]|3[0-1])(T([01][0-9]|2[0-3]):[0-5][0-9]:([0-5][0-9]|60)(\.[0-9]{1,9})?)?)?(Z|(\+|-)((0[0-9]|1[0-3]):[0-5][0-9]|14:00)?)?)?",
        RegexOptions.Compiled);

    private static readonly Regex _fileTargetRegex = new(
        @"[^\s]+\.(png|jpg|jpeg|gif|svg|htm|html|diagram|xsd|json|xml|sch|zip|shex|ttl)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        "dstu1", 
        "dstu2", "r2", "hl7.fhir.r2.core", "fhirVersion=1.0",
        "stu3", "r3", "hl7.fhir.r3.core", "fhirVersion=3.0",
        "r4", "hl7.fhir.r4.core", "fhirVersion=4.0",
        "r4b", "hl7.fhir.r4b.core", "fhirVersion=4.3",
        "r5", "hl7.fhir.r5.core", "hl7.fhir.r5.corexml", "hl7.fhir.r5.examples", "hl7.fhir.r5.expansions", "hl7.fhir.r5.search", "fhirVersion=5.0",
        ], StringComparer.OrdinalIgnoreCase);

    private string _fhirRepoPath;
    private IDbConnection? _db = null;
    private IDbConnection? _ciDb = null;

    public ContentReview(CliConfig config)
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
            _fhirCiCodes = loadFhirCodes(ciDb);
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
            _fhirCiCodes = Array.Empty<string>().ToFrozenSet();
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
            _fhirPublishedCodes = loadFhirCodes(fhirDb);
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
            _fhirPublishedCodes = Array.Empty<string>().ToFrozenSet();
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
            (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
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
            (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
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
            (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
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
            (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
            names.Add(name);
        }
        return names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private FrozenSet<string> loadFhirCodes(IDbConnection db)
    {
        HashSet<string> names = new(StringComparer.Ordinal);

        IDbCommand command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbCodeSystemConcept.Code)}" +
            $" FROM {CgDbCodeSystemConcept.DefaultTableName}" +
            $" WHERE {nameof(CgDbCodeSystemConcept.Code)} IS NOT NULL;";
        using (IDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
                names.Add(name);
            }
        }

        command = db.CreateCommand();
        command.CommandText = $"SELECT DISTINCT {nameof(CgDbValueSetConcept.Code)}" +
            $" FROM {CgDbValueSetConcept.DefaultTableName}" +
            $" WHERE {nameof(CgDbValueSetConcept.Code)} IS NOT NULL;";
        using (IDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
                names.Add(name);
            }
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
            (string path, _, _) = sanitizeAsKeyword(reader.GetString(0));
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
            (string name, _, _) = sanitizeAsKeyword(reader.GetString(0));
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
            // note that we cannot sanitize here, as that fixes many typos
            string typo = reader.GetString(0).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(typo))
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
            (string word, char firstLetter, _) = sanitizeAsKeyword(reader.GetString(0));
            if (firstLetter == '\0')
            {
                continue;
            }

            words.Add(word);
        }

        return words.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public void ProcessArtifacts()
    {
        if (!_haveCiDb)
        {
            throw new InvalidOperationException("CI FHIR database is required to process artifacts.");
        }

        Console.WriteLine("Processing FHIR resources...");
        Console.WriteLine($"Using database: {_config.DbPath}");
        Console.WriteLine($"Using FHIR repository path: {_config.FhirRepoPath}");

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

        // ensure our tables exist
        if (_config.DropTables)
        {
            ArtifactRecord.DropTable(_db);

            SpecPageRecord.Delete(_db, ArtifactIdIsNull: false);
            cleanDeletedPageRecords();
        }

        ArtifactRecord.CreateTable(_db);
        SpecPageRecord.CreateTable(_db);

        // load the initial set of resources we are working with
        List<ArtifactRecord> artifacts = buildArtifactList();
        // traverse the resources and perform review checks
        foreach (ArtifactRecord artifact in artifacts)
        {
            Console.WriteLine($"Processing artifact: '{artifact.FhirId}': {artifact.ArtifactType} ({artifact.DefinitionArtifactType})...");
            doArtifactReview(artifact);
        }

        _db.Close();
        _db = null;
        _ciDb.Close();
        _ciDb = null;
    }

    private void cleanDeletedPageRecords()
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }

        {
            IDbCommand command = _db.CreateCommand();
            command.CommandText = 
                $"DELETE from {SpecPageImageRecord.DefaultTableName}" +
                $" where {nameof(SpecPageImageRecord.PageId)} NOT IN (SELECT {nameof(SpecPageRecord.Id)} FROM {SpecPageRecord.DefaultTableName})";
            command.ExecuteNonQuery();
        }

        {
            IDbCommand command = _db.CreateCommand();
            command.CommandText =
                $"DELETE from {SpecPageRemovedFhirArtifactRecord.DefaultTableName}" +
                $" where {nameof(SpecPageRemovedFhirArtifactRecord.PageId)} NOT IN (SELECT {nameof(SpecPageRecord.Id)} FROM {SpecPageRecord.DefaultTableName})";
            command.ExecuteNonQuery();
        }

        {
            IDbCommand command = _db.CreateCommand();
            command.CommandText =
                $"DELETE from {SpecPageUnknownWordRecord.DefaultTableName}" +
                $" where {nameof(SpecPageUnknownWordRecord.PageId)} NOT IN (SELECT {nameof(SpecPageRecord.Id)} FROM {SpecPageRecord.DefaultTableName})";
            command.ExecuteNonQuery();
        }
    }

    private void doArtifactReview(ArtifactRecord artifact)
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }
        if (_ciDb is null)
        {
            throw new InvalidOperationException("CI FHIR database is not available.");
        }

        // get the db record for this artifact
        CgDbStructure? structure = CgDbStructure.SelectSingle(_ciDb, Id: artifact.FhirId);

        if (structure is null)
        {
            throw new Exception($"Structure not found in CI database for artifact '{artifact.FhirId}'.");
        }

        ArtifactRecord modified = artifact with { };

        // if this artifact has an unknown disposition, check if there is an FMG value
        if (modified.ContentDisposition == ContentDispositionCodes.Unknown)
        {
            FmgSheetContentRecord? fmgRec = FmgSheetContentRecord.SelectSingle(
                _db,
                Name: modified.Name);

            if (fmgRec is not null)
            {
                // check current disposition
                switch (fmgRec.Track?.ToLowerInvariant())
                {
                    case "n":
                        modified.ContentDisposition = ContentDispositionCodes.CoreAsNormative;
                        modified.DispositionVotedByWorkgroup = !string.IsNullOrWhiteSpace(fmgRec.VotedByWorkgroup);
                        break;

                    case "ar":
                        modified.ContentDisposition = ContentDispositionCodes.MoveToGuide;
                        modified.DispositionVotedByWorkgroup = !string.IsNullOrWhiteSpace(fmgRec.VotedByWorkgroup);
                        modified.DispositionLocation = fmgRec.Target;
                        break;
                }

                modified.ManagementComments = fmgRec.Notes;
            }
        }

        (string? expectedDir, string? expectedDefinitionFile) = getExpectedLocations(modified, structure);

        if (expectedDir is null)
        {
            modified.SourceDirectoryExists = false;
            modified.SourceDefinitionExists = false;
            modified.IntroPageFilename = null;
            modified.NotesPageFilename = null;
        }
        else
        {
            modified.SourceDirectoryExists = Directory.Exists(expectedDir);

            if ((expectedDefinitionFile is null) || (modified.SourceDirectoryExists != true))
            {
                modified.SourceDefinitionExists = false;
                modified.IntroPageFilename = null;
                modified.NotesPageFilename = null;
            }
            else
            {
                modified.SourceDefinitionExists = File.Exists(expectedDefinitionFile);

                string shortName = Path.GetFileNameWithoutExtension(expectedDefinitionFile).ToLowerInvariant();

                if (shortName.StartsWith("structuredefinition-", StringComparison.Ordinal))
                {
                    shortName = shortName["structuredefinition-".Length..];
                }

                string introFilename = $"{shortName}-introduction.xml";
                string notesFilename = $"{shortName}-notes.xml";

                modified.IntroPageFilename = File.Exists(Path.Combine(expectedDir, introFilename))
                    ? introFilename
                    : null;
                modified.NotesPageFilename = File.Exists(Path.Combine(expectedDir, notesFilename))
                    ? notesFilename
                    : null;
            }
        }

        modified.ResponsibleWorkGroup = structure.WorkGroup;
        modified.Status = structure.Status;
        modified.MaturityLevel = structure.FhirMaturity;
        modified.StandardsStatus = structure.StandardStatus;

        modified.Update(_db);

        if ((expectedDir is not null) && (modified.IntroPageFilename is not null))
        {
            SpecPageRecord? introPage = doArtifactPageReview(
                modified,
                expectedDir,
                modified.IntroPageFilename);
        }

        if ((expectedDir is not null) && (modified.NotesPageFilename is not null))
        {
            SpecPageRecord? notesPage = doArtifactPageReview(
                modified,
                expectedDir,
                modified.NotesPageFilename);
        }
    }

    private (string? sourceDir, string? definitionFile) getExpectedLocations(
        ArtifactRecord artifact,
        CgDbStructure? structure)
    {
        switch (artifact.ArtifactType?.ToLowerInvariant())
        {
            case "interface":
            case "resource":
                return (
                    Path.Combine(_fhirRepoPath, "source", artifact.FhirId.ToLowerInvariant()),
                    Path.Combine(_fhirRepoPath, "source", artifact.FhirId.ToLowerInvariant(), $"structuredefinition-{artifact.FhirId}.xml"));

            case "profile":
                {
                    // for profiles, we need the structure information to get the file location
                    if ((structure is null) || (structure.BaseDefinitionShort is null))
                    {
                        return (null, null);
                    }

                    return (
                        Path.Combine(_fhirRepoPath, "source", structure.BaseDefinitionShort.ToLowerInvariant()),
                        Path.Combine(
                            _fhirRepoPath, 
                            "source", 
                            structure.BaseDefinitionShort.ToLowerInvariant(), 
                            $"{structure.BaseDefinitionShort.ToLowerInvariant()}-{artifact.FhirId}.xml"));
                }

            case "primitivetype":
            case "primitive-type":
            case "complextype":
            case "complex-type":
            default:
                return (null, null);
        }
    }

    private SpecPageRecord doArtifactPageReview(ArtifactRecord artifact, string artifactDirectory, string sourceFilename)
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }
        if (_ciDb is null)
        {
            throw new InvalidOperationException("CI FHIR database is not available.");
        }

        string fullFilename = Path.Combine(artifactDirectory, sourceFilename);

        // check to see if there is a page record already
        SpecPageRecord? page = SpecPageRecord.SelectSingle(_db, ArtifactId: artifact.Id, PageFileName: sourceFilename);
        if (page is null)
        {
            page = new()
            {
                Id = SpecPageRecord.GetIndex(),
                ArtifactId = artifact.Id,
                FhirArtifactId = artifact.FhirId,
                PageFileName = sourceFilename,
                ExistsInPublishIni = null,
                ExistsInSource = File.Exists(fullFilename),
                ResponsibleWorkGroup = artifact.ResponsibleWorkGroup,
                MaturityLabel = artifact.Status,
                MaturityLevel = artifact.MaturityLevel,
                StandardsStatus = artifact.StandardsStatus,
            };

            page.Insert(_db);
        }
        else if (!File.Exists(fullFilename))
        {
            page.ExistsInSource = false;
            page.Update(_db);
            return page;
        }
        else
        {
            page.ExistsInSource = true;
        }

        Console.WriteLine($"    Processing {artifact.FhirId} page: '{page.PageFileName}'...");
        doPageReview(page, fullFilename);

        return page;
    }

    private List<ArtifactRecord> buildArtifactList()
    {
        if (_db is null)
        {
            throw new InvalidOperationException("Database connection is not initialized.");
        }

        if (_ciDb is null)
        {
            throw new InvalidOperationException("CI FHIR database is not available.");
        }

        List<ArtifactRecord> artifacts = ArtifactRecord.SelectList(_db);
        List<CgDbStructure> structures = CgDbStructure.SelectList(_ciDb);
        List<ArtifactRecord> toInsert = [];
        ILookup<string, ArtifactRecord> dbArtifactLookup = artifacts.ToLookup(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (CgDbStructure structure in structures)
        {
            if (!dbArtifactLookup.Contains(structure.Name))
            {
                ArtifactRecord artifact = new()
                {
                    Id = ArtifactRecord.GetIndex(),
                    FhirId = structure.Id,
                    Name = structure.Name,
                    DefinitionArtifactType = "StructureDefinition",
                    ArtifactType = structure.ArtifactClass,
                };
                artifacts.Add(artifact);
                toInsert.Add(artifact);
            }
        }

        if (toInsert.Count > 0)
        {
            toInsert.Insert(_db, ignoreDuplicates: false, insertPrimaryKey: true);
        }

        return artifacts;
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
            string sourceFile = Path.Combine(_fhirRepoPath, "source", page.PageFileName);
            doPageReview(page, sourceFile);
        }
    }

    private void doPageReview(SpecPageRecord page, string fullFilename)
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

        if (!File.Exists(fullFilename))
        {
            Console.WriteLine($"Source file not found for page {page.PageFileName}");
            modified.ExistsInSource = false;
            modified.Update(_db);
            return;
        }

        try
        {
            // read the source file
            string htmlContent = File.ReadAllText(fullFilename);

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

            //string visibleText = extractVisibleText(doc);
            string visibleText = stripHtml(doc.Body?.TextContent ?? string.Empty);

            int footerLoc = visibleText.IndexOf("[%file newfooter%]", StringComparison.Ordinal);
            if (footerLoc != -1)
            {
                visibleText = visibleText[0..footerLoc];
            }

            // perform necessary checks root page checks
            if (page.ArtifactId is null)
            {
                bool isStandardPage = true;
                modified.ResponsibleWorkGroup = extractWorkGroup(doc);
                (isStandardPage, modified.MaturityLabel, modified.MaturityLevel, modified.StandardsStatus) = extractStatusInfo(doc);
                if (!isStandardPage)
                {
                    return;
                }
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

            int priorFhirVersionCount = 0;
            int deprecatedLiteralCount = 0;

            // do word-processing on pages other than the credits artifact
            if (page.PageFileName != "credits.html")
            {
                processWords(page, visibleText, ref priorFhirVersionCount, ref deprecatedLiteralCount);
            }

            // update word-based totals
            modified.RemovedFhirArtifactCount = SpecPageRemovedFhirArtifactRecord.SelectCount(_db, PageId: page.Id);
            modified.UnknownWordCount = SpecPageUnknownWordRecord.SelectCount(_db, PageId: page.Id, IsTypo: false);
            modified.TypoWordCount = SpecPageUnknownWordRecord.SelectCount(_db, PageId: page.Id, IsTypo: true);
            modified.PriorFhirVersionReferenceCount = priorFhirVersionCount;
            modified.DeprecatedLiteralCount = deprecatedLiteralCount;

            // perform image checks
            modified.ImagesWithIssuesCount = checkPageImages(doc, page.Id);

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

    private void processWords(
        SpecPageRecord page, 
        string visibleText, 
        ref int priorFhirVersionCount, 
        ref int deprecatedLiteralCount)
    {
        string? lastArtifactName = null;

        // process the visible text into words
        string[] words = visibleText.Split(_wordSplitChars, StringSplitOptions.RemoveEmptyEntries);

        foreach ((string word, int wordIndex) in words.Select((w, i) => (w, i)))
        {
            // skip URLs, email addresses, etc.
            if (_urlRegex.IsMatch(word) ||
                _baseFhirRegex.IsMatch(word) ||
                _typeFhirRegex.IsMatch(word) ||
                _emailAddressRegex.IsMatch(word) ||
                _urnRegex.IsMatch(word) ||
                _xsdRegex.IsMatch(word) ||
                _fhirShexRegex.IsMatch(word) ||
                _thoCodeSystemRegex.IsMatch(word) ||
                _fileTargetRegex.IsMatch(word) ||
                _dateTimeRegex.IsMatch(word))
            {
                continue;
            }

            if (word.StartsWith("[%", StringComparison.Ordinal) ||
                word.EndsWith("%]", StringComparison.Ordinal))
            {
                // skip special markup words
                continue;
            }

            (string sanitized, char firstLetter, char? prefixSymbol) = sanitizeAsKeyword(word);

            // words that start with a '%' symbol are FHIRPath variables in this context, and there is no list of them
            // words that start with a '#' symbol are fragments or codes, neither of which we are interested in
            if ((prefixSymbol == '%') ||
                (prefixSymbol == '#'))
            {
                continue;
            }

            // check for relative url paths
            if ((prefixSymbol == '/') && word.StartsWith('/'))
            {
                continue;
            }

            // skip words that do not have letters in them (e.g., numbers)
            if (firstLetter == '\0')
            {
                continue;
            }

            (bool wordHasDisposition, string? wordArtifactName) = processWord(
                page,
                ref priorFhirVersionCount,
                ref deprecatedLiteralCount,
                word,
                sanitized,
                firstLetter,
                prefixSymbol,
                lastArtifactName);

            if (wordHasDisposition)
            {
                lastArtifactName = wordArtifactName ?? lastArtifactName;
                continue;
            }

            // split the word on extended characters so we can process each component individually now
            string[] subWords = word.Split(
                _extendedSplitChars,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string subWord in subWords)
            {
                (string subSanitized, char subFirstLetter, char? subPrefixSymbol) = sanitizeAsKeyword(subWord);

                // skip words that do not have letters in them (e.g., numbers)
                if (subFirstLetter == '\0')
                {
                    continue;
                }

                (wordHasDisposition, wordArtifactName) = processWord(
                    page,
                    ref priorFhirVersionCount,
                    ref deprecatedLiteralCount,
                    subWord,
                    subSanitized,
                    subFirstLetter,
                    subPrefixSymbol,
                    lastArtifactName);

                if (wordHasDisposition)
                {
                    lastArtifactName = wordArtifactName ?? lastArtifactName;
                    continue;
                }

                // check to see if we have an existing record for this word
                SpecPageUnknownWordRecord? unknownWord = SpecPageUnknownWordRecord.SelectSingle(
                    _db!,
                    PageId: page.Id,
                    Word: word);
                if (unknownWord is null)
                {
                    // new record
                    unknownWord = new SpecPageUnknownWordRecord
                    {
                        Id = SpecPageUnknownWordRecord.GetIndex(),
                        PageId = page.Id,
                        Word = word,
                        IsTypo = false,
                    };
                    unknownWord.Insert(_db!, ignoreDuplicates: false, insertPrimaryKey: true);
                }
            }
        }
    }

    /// <summary>
    /// Process a word
    /// </summary>
    /// <param name="page"></param>
    /// <param name="priorFhirVersionCount"></param>
    /// <param name="deprecatedLiteralCount"></param>
    /// <param name="word"></param>
    /// <param name="sanitized"></param>
    /// <param name="firstLetter"></param>
    /// <param name="prefixSymbol"></param>
    /// <returns>true if the word has been positively identified, false otherwise</returns>
    private (bool hasDisposition, string? resourceName) processWord(
        SpecPageRecord page, 
        ref int priorFhirVersionCount, 
        ref int deprecatedLiteralCount, 
        string word, 
        string sanitized, 
        char firstLetter, 
        char? prefixSymbol,
        string? lastArtifactName)
    {
        // check to see if this word is a prior FHIR version keyword
        if (_priorFhirVersionKeywords.Contains(sanitized))
        {
            priorFhirVersionCount++;
            return (true, null);
        }

        // check to see if this is the word 'deprecated'
        if (string.Equals(sanitized, "deprecated", StringComparison.Ordinal))
        {
            deprecatedLiteralCount++;
            return (true, null);
        }

        bool inCi = false;
        string? ciArtifactClass = null;
        string? ciArtifactName = null;

        bool inPublished = false;
        string? publishedArtifactClass = null;
        string? publishedArtifactName = null;

        string? lastArtifactWord = (lastArtifactName is not null) && (prefixSymbol == '.')
            ? lastArtifactName + word
            : null;

        (string? lastArtifactSanitized, _, _) = lastArtifactWord is null
            ? (null, '\0', null)
            : sanitizeAsKeyword(lastArtifactWord);

        // check if this word is a FHIR artifact in the current ci
        if (_haveCiDb)
        {
            (inCi, ciArtifactClass, ciArtifactName) = testWordAgainstFhir(
                word,
                sanitized, 
                firstLetter, 
                prefixSymbol, 
                FhirSourceCodes.Ci);

            if (inCi)
            {
                return (true, ciArtifactName);
            }

            // check for this being a relative path to a known artifact
            if ((lastArtifactWord is not null) && (lastArtifactSanitized is not null))
            {
                (inCi, ciArtifactClass, ciArtifactName) = testWordAgainstFhir(
                    lastArtifactWord,
                    lastArtifactSanitized,
                    firstLetter,
                    null,
                    FhirSourceCodes.Ci);
                if (inCi)
                {
                    return (true, ciArtifactName);
                }
            }

            // try non-plural version of the word if it ends with 's'
            if (sanitized.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                (inCi, ciArtifactClass, ciArtifactName) = testWordAgainstFhir(
                    word[..^1],
                    sanitized[..^1],
                    firstLetter,
                    prefixSymbol,
                    FhirSourceCodes.Ci);
                if (inCi)
                {
                    return (true, ciArtifactName);
                }
            }
        }

        // check if this word is a FHIR artifact in the published spec
        if (_havePublishedDb)
        {
            (inPublished, publishedArtifactClass, publishedArtifactName) = testWordAgainstFhir(
                word,
                sanitized, 
                firstLetter, 
                prefixSymbol, 
                FhirSourceCodes.Published);

            if (inPublished)
            {
                lastArtifactName = publishedArtifactName;
            }
            // check for this being a relative path to a known artifact
            else if (lastArtifactWord is not null && lastArtifactSanitized is not null)
            {
                (inPublished, publishedArtifactClass, publishedArtifactName) = testWordAgainstFhir(
                    lastArtifactWord,
                    lastArtifactSanitized,
                    firstLetter,
                    null,
                    FhirSourceCodes.Published);

                if (inPublished)
                {
                    lastArtifactName = publishedArtifactName;
                }
            }

            // try non-plural version of the word if it ends with 's'
            if (!inPublished && sanitized.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                (inPublished, publishedArtifactClass, publishedArtifactName) = testWordAgainstFhir(
                    word[..^1],
                    sanitized[..^1],
                    firstLetter,
                    prefixSymbol,
                    FhirSourceCodes.Published);
                if (inPublished)
                {
                    lastArtifactName = publishedArtifactName;
                }
            }
        }

        // check if this is a 'FHIR' term that has been removed
        if (inPublished && !inCi)
        {
            // before adding as a 'removed' word, check to see if it is in the dictionary (e.g., Conformance)
            if (_dictionaryWords.Contains(sanitized))
            {
                // known good word, nothing to do
                return (true, null);
            }

            // check to see if we have an existing record for this word
            SpecPageRemovedFhirArtifactRecord? removedWord = SpecPageRemovedFhirArtifactRecord.SelectSingle(
                _db!,
                PageId: page.Id,
                Word: word);

            if (removedWord is null)
            {
                // new record
                removedWord = new SpecPageRemovedFhirArtifactRecord
                {
                    Id = SpecPageRemovedFhirArtifactRecord.GetIndex(),
                    PageId = page.Id,
                    Word = word,
                    ArtifactClass = publishedArtifactClass ?? "Unknown",
                };

                removedWord.Insert(_db!, ignoreDuplicates: false, insertPrimaryKey: true);
            }

            return (true, publishedArtifactName);
        }

        // check if this is an unknown word
        if (!inCi &&
            !inPublished &&
            _haveDictDb)
        {
            // check typo list first
            if (_typoWords.ContainsKey(word) ||
                _typoWords.ContainsKey(sanitized))
            {
                // check to see if we have an existing record for this word
                SpecPageUnknownWordRecord? typoRecord = SpecPageUnknownWordRecord.SelectSingle(
                    _db!,
                    PageId: page.Id,
                    Word: word);

                if (typoRecord is null)
                {
                    // new record
                    typoRecord = new SpecPageUnknownWordRecord
                    {
                        Id = SpecPageUnknownWordRecord.GetIndex(),
                        PageId = page.Id,
                        Word = word,
                        IsTypo = true,
                    };
                    typoRecord.Insert(_db!, ignoreDuplicates: false, insertPrimaryKey: true);
                }

                return (true, null);
            }

            // check word list next
            if (_dictionaryWords.Contains(sanitized))
            {
                // known good word, nothing to do
                return (true, null);
            }
        }

        return (false, null);
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

    private (bool found, string? artifactClass, string? artifactName) testWordAgainstFhir(
        string word,
        string sanitized, 
        char firstLetter, 
        char? prefixSymbol,
        FhirSourceCodes comparisonSource)
    {
        if (firstLetter == '\0')
        {
            return (false, null, null);
        }

        FrozenDictionary<string, string> structures;
        FrozenSet<string> elementPaths;
        FrozenSet<string> valueSetNames;
        FrozenSet<string> codeSystemNames;
        FrozenSet<string> codes;
        FrozenSet<string> operationNames;
        FrozenSet<string> searchParameterNames;

        switch (comparisonSource)
        {
            case FhirSourceCodes.Ci:
                structures = _fhirCiStructures;
                elementPaths = _fhirCiElementPaths;
                valueSetNames = _fhirCiValueSetNames;
                codeSystemNames = _fhirCiCodeSystemNames;
                codes = _fhirCiCodes;
                operationNames = _fhirCiOperationNames;
                searchParameterNames = _fhirCiSearchParameterNames;
                break;

            case FhirSourceCodes.Published:
                structures = _fhirPublishedStructures;
                elementPaths = _fhirPublishedElementPaths;
                valueSetNames = _fhirPublishedValueSetNames;
                codeSystemNames = _fhirPublishedCodeSystemNames;
                codes = _fhirPublishedCodes;
                operationNames = _fhirPublishedOperationNames;
                searchParameterNames = _fhirPublishedSearchParameterNames;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(comparisonSource), comparisonSource, null);
        }

        //// check if this is supposed to be an operation
        //if (prefixSymbol == '$')
        //{
        //    if (_fhirCiOperationNames.Contains(sanitized))
        //    {
        //        return (true, "Operation");
        //    }

        //    // fail anything else that starts with '$' - there are no valid published artifacts that start with '$' other than operations
        //    return (false, null);
        //}

        // check if there is a structure definition for this 'word'
        if (structures.TryGetValue(sanitized, out string? artifactClass))
        {
            return (true, artifactClass, word.Split('.', StringSplitOptions.RemoveEmptyEntries)[0].Trim());
        }

        if (sanitized.StartsWith("value", StringComparison.Ordinal) &&
            structures.TryGetValue(sanitized[5..], out artifactClass))
        {
            return (true, artifactClass, null);
        }

        // check if there is an element for this 'word'
        if (elementPaths.Contains(sanitized))
        {
            return (true, "Element", word.Split('.', StringSplitOptions.RemoveEmptyEntries)[0].Trim());
        }

        //// check if there is a code for this 'word' - restict to # prefix because almost all words are codes otherwise
        //if ((prefixSymbol == '#') &&
        //    codes.Contains(sanitized))
        //{
        //    return (true, "Code");
        //}

        //// check if there is a value set for this 'word'
        //if (valueSetNames.Contains(sanitized))
        //{
        //    return (true, "ValueSet");
        //}

        //// check if there is a code system for this 'word'
        //if (codeSystemNames.Contains(sanitized))
        //{
        //    return (true, "CodeSystem");
        //}

        // check if there is a search parameter for this 'word' - restrict to _ prefixes because too many words are search parameters otherwise
        if ((prefixSymbol == '_') &&
            searchParameterNames.Contains(sanitized))
        {
            return (true, "SearchParameter", null);
        }

        return (false, null, null);
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

            // format of text is `Work Group[%wg vocab%]`
            string[] parts = content.Split(_wgSplitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            int wgLiteralIndex = Array.FindIndex(
                parts, 
                p => p.Equals("wg", StringComparison.OrdinalIgnoreCase) || p.Equals("wgt", StringComparison.OrdinalIgnoreCase));

            if ((wgLiteralIndex != -1) &&
                (parts.Length > wgLiteralIndex + 1))
            {
                return parts[wgLiteralIndex + 1];
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
                    Id = SpecPageRecord.GetIndex(),
                    PageFileName = iniPage,
                    ExistsInPublishIni = true,
                    ExistsInSource = File.Exists(Path.Combine(_fhirRepoPath, "source", iniPage)),
                    ArtifactId = null,
                    FhirArtifactId = null,
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
        return _htmlStripRegex.Replace(text, " ");
    }

    private static (string clean, char firstLetter, char? prefixSymbol) sanitizeAsKeyword(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (string.Empty, '\0', null);
        }

        char? firstLetter = null;
        char? prefixSymbol = null;

        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);

            switch (uc)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    sb.Append(char.ToLower(c));
                    firstLetter ??= c;
                    break;

                case UnicodeCategory.LowercaseLetter:
                    firstLetter ??= c;
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
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherPunctuation:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                //case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.OtherSymbol:
                    //case UnicodeCategory.OtherNotAssigned:
                    if (firstLetter == null)
                    {
                        prefixSymbol ??= c;
                    }

                    break;
                default:
                    break;
            }
        }
        return (sb.ToString(), firstLetter ?? '\0', prefixSymbol);
    }

}
