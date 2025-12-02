using JiraFhirUtils.Common;
using JiraFhirUtils.Common.FhirDbModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace jira_fhir_cli.Keyword;

public record SearchResult
{
    public required int IssueId { get; set; }
    public required double Score { get; set; }
    public List<string> MatchingTerms { get; set; } = new();
    public IssueRecord? Issue { get; set; }
}

public class Bm25SearchEngine
{
    private readonly SqliteConnection _db;
    private readonly FrozenSet<string> _stopWords;
    private readonly FrozenDictionary<string, string> _lemmas;
    private readonly FrozenSet<string> _fhirElementPaths;
    private readonly FrozenSet<string> _fhirOperationNames;

    public Bm25SearchEngine(
        SqliteConnection db,
        FrozenSet<string> stopWords,
        FrozenDictionary<string, string> lemmas,
        FrozenSet<string> fhirElementPaths,
        FrozenSet<string> fhirOperationNames)
    {
        _db = db;
        _stopWords = stopWords;
        _lemmas = lemmas;
        _fhirElementPaths = fhirElementPaths;
        _fhirOperationNames = fhirOperationNames;
    }

    public List<SearchResult> SearchIssues(string query, int topK = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        Console.WriteLine($"Searching for: '{query}' (top {topK} results)");

        List<string> queryTerms = ParseQuery(query);
        Console.WriteLine($"Query terms: {string.Join(", ", queryTerms)}");

        if (queryTerms.Count == 0)
        {
            Console.WriteLine("No valid query terms found.");
            return [];
        }

        Dictionary<int, SearchResult> issueScores = new();
        
        foreach (string term in queryTerms)
        {
            processQueryTerm(term, issueScores);
        }

        List<SearchResult> results = issueScores.Values
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        loadIssueDetails(results);

        Console.WriteLine($"Found {results.Count} matching issues.");
        return results;
    }

    private List<string> ParseQuery(string query)
    {
        List<string> terms = new();
        
        string[] words = query.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string word in words)
        {
            (string sanitized, char firstLetter, char? prefixSymbol) = KeywordProcessor.SanitizeAsKeyword(word);
            
            if ((firstLetter == '\0') || (sanitized.Length < 3))
            {
                continue;
            }

            if (_stopWords.Contains(sanitized))
            {
                continue;
            }

            bool isFhirElementPath = _fhirElementPaths.Contains(sanitized);
            bool isFhirOperationName = (prefixSymbol == '$') && _fhirOperationNames.Contains(sanitized);
            bool isLemma = _lemmas.TryGetValue(sanitized, out string? lemma);

            bool processAsFhirElementPath = isFhirElementPath &&
                (!isLemma || (isLemma && char.IsUpper(firstLetter)));

            if (processAsFhirElementPath)
            {
                terms.Add(sanitized);
            }
            else if (isFhirOperationName)
            {
                terms.Add(sanitized);
            }
            else if (isLemma && !string.IsNullOrWhiteSpace(lemma))
            {
                terms.Add(lemma);
            }
            else
            {
                terms.Add(sanitized);
            }
        }

        return terms.Distinct().ToList();
    }

    private void processQueryTerm(string term, Dictionary<int, SearchResult> issueScores)
    {
        List<DbIssueKeywordRecord> keywordMatches = getKeywordMatches(term);
        
        foreach (DbIssueKeywordRecord match in keywordMatches)
        {
            if (!match.Bm25Score.HasValue || match.Bm25Score.Value <= 0)
            {
                continue;
            }

            if (!issueScores.TryGetValue(match.IssueId, out SearchResult? result))
            {
                result = new SearchResult
                {
                    IssueId = match.IssueId,
                    Score = 0
                };
                issueScores[match.IssueId] = result;
            }

            result.Score += match.Bm25Score.Value;
            result.MatchingTerms.Add(term);
        }
    }

    private List<DbIssueKeywordRecord> getKeywordMatches(string term)
    {
        List<DbIssueKeywordRecord> matches = DbIssueKeywordRecord.SelectList(
            _db,
            Keyword: term,
            orderByProperties: [nameof(DbIssueKeywordRecord.Bm25Score)],
            orderByDirection: "DESC");

        return matches;
    }

    private void loadIssueDetails(List<SearchResult> results)
    {
        foreach (SearchResult result in results)
        {
            result.Issue = IssueRecord.SelectSingle(_db, Id: result.IssueId);
        }
    }

    public void PrintSearchResults(List<SearchResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("No results found.");
            return;
        }

        Console.WriteLine($"\nTop {results.Count} search results:");
        Console.WriteLine("".PadRight(80, '='));

        for (int i = 0; i < results.Count; i++)
        {
            SearchResult result = results[i];
            Console.WriteLine($"{i + 1:D2}. Issue {result.IssueId} (Score: {result.Score:F4})");
            
            if (result.Issue != null)
            {
                Console.WriteLine($"    Title: {result.Issue.Title ?? "N/A"}");
                Console.WriteLine($"    Status: {result.Issue.Status ?? "N/A"}");
                Console.WriteLine($"    Priority: {result.Issue.Priority ?? "N/A"}");
            }
            
            Console.WriteLine($"    Matching terms: {string.Join(", ", result.MatchingTerms)}");
            Console.WriteLine();
        }
    }

    public List<SearchResult> SearchByKeywordType(KeywordTypeCodes keywordType, int topK = 20)
    {
        Console.WriteLine($"Searching by keyword type: {keywordType} (top {topK} results)");

        using IDbCommand command = _db.CreateCommand();
        command.CommandText = @"
            SELECT IssueId, SUM(Bm25Score) as TotalScore, COUNT(*) as TermCount
            FROM issue_keywords 
            WHERE KeywordType = @keywordType AND Bm25Score IS NOT NULL AND Bm25Score > 0
            GROUP BY IssueId
            ORDER BY TotalScore DESC
            LIMIT @topK";
        
        var typeParam = command.CreateParameter();
        typeParam.ParameterName = "@keywordType";
        typeParam.Value = (int)keywordType;
        command.Parameters.Add(typeParam);

        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@topK";
        limitParam.Value = topK;
        command.Parameters.Add(limitParam);

        List<SearchResult> results = new();
        using IDataReader reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            results.Add(new SearchResult
            {
                IssueId = reader.GetInt32(0),
                Score = reader.GetDouble(1)
            });
        }

        loadIssueDetails(results);

        Console.WriteLine($"Found {results.Count} issues with {keywordType} keywords.");
        return results;
    }

    public Dictionary<string, double> GetTopKeywords(KeywordTypeCodes? keywordType = null, int topK = 50)
    {
        Console.WriteLine($"Getting top {topK} keywords" + (keywordType.HasValue ? $" of type {keywordType.Value}" : ""));

        using IDbCommand command = _db.CreateCommand();
        
        string sql = @"
            SELECT Keyword, Idf, Count
            FROM corpus_keywords 
            WHERE Idf IS NOT NULL";
        
        if (keywordType.HasValue)
        {
            sql += " AND KeywordType = @keywordType";
        }
        
        sql += @"
            ORDER BY Idf DESC, Count DESC
            LIMIT @topK";
        
        command.CommandText = sql;

        if (keywordType.HasValue)
        {
            var typeParam = command.CreateParameter();
            typeParam.ParameterName = "@keywordType";
            typeParam.Value = (int)keywordType.Value;
            command.Parameters.Add(typeParam);
        }

        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@topK";
        limitParam.Value = topK;
        command.Parameters.Add(limitParam);

        Dictionary<string, double> keywords = new();
        using IDataReader reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            string keyword = reader.GetString(0);
            double idf = reader.GetDouble(1);
            keywords[keyword] = idf;
        }

        return keywords;
    }
}