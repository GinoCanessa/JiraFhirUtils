using JiraFhirUtils.Common;
using JiraFhirUtils.Common.FhirDbModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace jira_fhir_cli.Keyword;

public class Bm25SearchProcessor
{
    private readonly CliConfig _config;
    private FrozenSet<string> _stopWords;
    private FrozenDictionary<string, string> _lemmas;
    private FrozenSet<string> _fhirElementPaths;
    private FrozenSet<string> _fhirOperationNames;

    public Bm25SearchProcessor(CliConfig config)
    {
        _config = config;
        
        _stopWords = loadStopWords(config.KeywordDatabase);
        _lemmas = loadLemmas(config.KeywordDatabase);
        (_fhirElementPaths, _fhirOperationNames) = loadFhirSpecContent(config.FhirSpecDatabase);
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting BM25 search...");
        Console.WriteLine($"Using database: {_config.DbPath}");

        if (!File.Exists(_config.DbPath))
        {
            Console.WriteLine($"Error: Database file '{_config.DbPath}' not found.");
            return;
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={_config.DbPath}");
        await db.OpenAsync();

        // Verify BM25 data exists
        if (!VerifyBm25DataExists(db))
        {
            Console.WriteLine("Error: BM25 data not found. Please run 'extract-keywords' command first.");
            return;
        }

        Bm25SearchEngine searchEngine = new(db, _stopWords, _lemmas, _fhirElementPaths, _fhirOperationNames);

        if (_config.ShowTopKeywords)
        {
            ShowTopKeywords(searchEngine);
        }

        if (!string.IsNullOrWhiteSpace(_config.SearchQuery))
        {
            PerformSearch(searchEngine);
        }
        else if (!string.IsNullOrWhiteSpace(_config.SearchKeywordType))
        {
            PerformKeywordTypeSearch(searchEngine);
        }
        else if (!_config.ShowTopKeywords)
        {
            Console.WriteLine("Error: Please provide either --query, --keyword-type, or --show-keywords option.");
            Console.WriteLine("Use --help for more information.");
        }
    }

    private bool VerifyBm25DataExists(SqliteConnection db)
    {
        try
        {
            using var command = db.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM corpus_keywords WHERE Idf IS NOT NULL;";
            
            object? result = command.ExecuteScalar();
            int count = result != null ? Convert.ToInt32(result) : 0;
            
            Console.WriteLine($"Found {count} keywords with IDF values.");
            return count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking BM25 data: {ex.Message}");
            return false;
        }
    }

    private void ShowTopKeywords(Bm25SearchEngine searchEngine)
    {
        Console.WriteLine("\n=== Top Keywords by IDF Score ===");

        KeywordTypeCodes? keywordTypeFilter = null;
        if (!string.IsNullOrWhiteSpace(_config.SearchKeywordType))
        {
            if (Enum.TryParse<KeywordTypeCodes>(_config.SearchKeywordType, true, out KeywordTypeCodes parsedType))
            {
                keywordTypeFilter = parsedType;
            }
            else
            {
                Console.WriteLine($"Warning: Invalid keyword type '{_config.SearchKeywordType}'. Showing all types.");
            }
        }

        var topKeywords = searchEngine.GetTopKeywords(keywordTypeFilter, _config.SearchTopK);

        if (topKeywords.Count == 0)
        {
            Console.WriteLine("No keywords found.");
            return;
        }

        Console.WriteLine($"\nTop {topKeywords.Count} keywords" + (keywordTypeFilter.HasValue ? $" of type {keywordTypeFilter.Value}" : "") + ":");
        Console.WriteLine("".PadRight(60, '='));

        int rank = 1;
        foreach (var (keyword, idf) in topKeywords)
        {
            Console.WriteLine($"{rank:D2}. {keyword.PadRight(30)} (IDF: {idf:F6})");
            rank++;
        }
    }

    private void PerformSearch(Bm25SearchEngine searchEngine)
    {
        Console.WriteLine($"\n=== BM25 Search Results ===");
        Console.WriteLine($"Query: '{_config.SearchQuery}'");
        
        var results = searchEngine.SearchIssues(_config.SearchQuery!, _config.SearchTopK);
        searchEngine.PrintSearchResults(results);
        
        if (results.Count > 0)
        {
            Console.WriteLine($"\nSearch completed. Found {results.Count} matching issues.");
            PrintSearchStatistics(results);
        }
    }

    private void PerformKeywordTypeSearch(Bm25SearchEngine searchEngine)
    {
        if (!Enum.TryParse<KeywordTypeCodes>(_config.SearchKeywordType, true, out KeywordTypeCodes keywordType))
        {
            Console.WriteLine($"Error: Invalid keyword type '{_config.SearchKeywordType}'. Valid types: {string.Join(", ", Enum.GetNames<KeywordTypeCodes>())}");
            return;
        }

        Console.WriteLine($"\n=== Issues by Keyword Type: {keywordType} ===");
        
        var results = searchEngine.SearchByKeywordType(keywordType, _config.SearchTopK);
        searchEngine.PrintSearchResults(results);
        
        if (results.Count > 0)
        {
            Console.WriteLine($"\nSearch completed. Found {results.Count} issues with {keywordType} keywords.");
            PrintSearchStatistics(results);
        }
    }

    private static void PrintSearchStatistics(List<SearchResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        double maxScore = results[0].Score;
        double minScore = results[^1].Score;
        double avgScore = results.Select(r => r.Score).Average();
        
        Console.WriteLine("\nSearch Statistics:");
        Console.WriteLine($"  Max Score: {maxScore:F4}");
        Console.WriteLine($"  Min Score: {minScore:F4}");
        Console.WriteLine($"  Avg Score: {avgScore:F4}");
        
        var statusCounts = results.Where(r => r.Issue != null)
            .GroupBy(r => r.Issue!.Status ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());
        
        if (statusCounts.Count > 0)
        {
            Console.WriteLine("\nStatus Distribution:");
            foreach (var (status, count) in statusCounts.OrderByDescending(kvp => kvp.Value))
            {
                Console.WriteLine($"  {status}: {count}");
            }
        }
    }

    private FrozenDictionary<string, string> loadLemmas(string auxDbPath)
    {
        Dictionary<string, string> lemmas = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(auxDbPath) || !File.Exists(auxDbPath))
        {
            Console.WriteLine($"Warning: Auxiliary database file '{auxDbPath}' does not exist. No lemmas will be used.");
            return lemmas.ToFrozenDictionary(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={auxDbPath}");
        db.Open();

        List<LemmaRecord> lemmaRecords = LemmaRecord.SelectList(db);
        foreach (LemmaRecord record in lemmaRecords)
        {
            (string sanitized, char firstLetter, _) = KeywordProcessor.SanitizeAsKeyword(record.Inflection);
            if ((firstLetter == '\0') || (sanitized.Length < 3) || lemmas.ContainsKey(sanitized))
            {
                continue;
            }

            lemmas[sanitized] = record.Lemma;
        }

        return lemmas.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private FrozenSet<string> loadStopWords(string auxDbPath)
    {
        HashSet<string> words = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(auxDbPath) || !File.Exists(auxDbPath))
        {
            Console.WriteLine($"Warning: Auxiliary database file '{auxDbPath}' does not exist. No stop words will be used.");
            return words.ToFrozenSet(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={auxDbPath}");
        db.Open();

        using var command = db.CreateCommand();
        command.CommandText = "SELECT DISTINCT word FROM stop_words WHERE word IS NOT NULL;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            (string word, char firstLetter, _) = KeywordProcessor.SanitizeAsKeyword(reader.GetString(0));
            if (firstLetter != '\0')
            {
                words.Add(word);
            }
        }

        return words.ToFrozenSet(StringComparer.Ordinal);
    }

    private (FrozenSet<string> elementPaths, FrozenSet<string> operationNames) loadFhirSpecContent(string? fhirSpecDbPath)
    {
        HashSet<string> paths = new(StringComparer.Ordinal);
        HashSet<string> operations = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(fhirSpecDbPath) || !File.Exists(fhirSpecDbPath))
        {
            Console.WriteLine($"Warning: FHIR spec database file '{fhirSpecDbPath}' does not exist. No FHIR element paths will be used.");
            return (paths.ToFrozenSet(StringComparer.Ordinal), operations.ToFrozenSet(StringComparer.Ordinal));
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={fhirSpecDbPath}");
        db.Open();

        // Load element paths
        using var pathCommand = db.CreateCommand();
        pathCommand.CommandText = $"SELECT DISTINCT {nameof(CgDbElement.Path)} FROM {CgDbElement.DefaultTableName} WHERE {nameof(CgDbElement.Path)} IS NOT NULL;";
        using var pathReader = pathCommand.ExecuteReader();
        while (pathReader.Read())
        {
            (string path, char firstLetter, _) = KeywordProcessor.SanitizeAsKeyword(pathReader.GetString(0));
            if (firstLetter != '\0')
            {
                paths.Add(path);
            }
        }

        // Load operation names
        using var opCommand = db.CreateCommand();
        opCommand.CommandText = $"SELECT DISTINCT {nameof(CgDbOperation.Code)} FROM {CgDbOperation.DefaultTableName} WHERE {nameof(CgDbOperation.Code)} IS NOT NULL;";
        using var opReader = opCommand.ExecuteReader();
        while (opReader.Read())
        {
            (string code, char firstLetter, _) = KeywordProcessor.SanitizeAsKeyword(opReader.GetString(0));
            if (firstLetter != '\0')
            {
                operations.Add(code);
            }
        }

        return (paths.ToFrozenSet(StringComparer.Ordinal), operations.ToFrozenSet(StringComparer.Ordinal));
    }
}