using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.Common.FhirDbModels;

namespace jira_fhir_cli.Keyword;

public class KeywordProcessor
{
    private const int _minKeywordLength = 3;
    private const int _keywordsPerIssue = 10;
    
    private static System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly char[] _wordSplitChars = [' ', '\t', '\r', '\n'];

    private CliConfig _config;
    private FrozenSet<string> _stopWords;
    private FrozenDictionary<string, string> _lemmas;
    private FrozenSet<string> _fhirElementPaths;
    private FrozenSet<string> _fhirOperationNames;

    public KeywordProcessor(CliConfig config)
    {
        _config = config;

        _stopWords = loadStopWords(config.KeywordDatabase);
        Console.WriteLine($"Loaded {_stopWords.Count} stopwords from '{config.KeywordDatabase}'.");

        _lemmas = loadLemmas(config.KeywordDatabase);
        Console.WriteLine($"Loaded {_lemmas.Count} lemmas from '{config.KeywordDatabase}'.");

        (_fhirElementPaths, _fhirOperationNames) = loadFhirSpecContent(config.FhirSpecDatabase);
        Console.WriteLine($"Loaded {_fhirElementPaths.Count} FHIR element paths from '{config.FhirSpecDatabase}'.");
        Console.WriteLine($"Loaded {_fhirOperationNames.Count} FHIR operation names from '{config.FhirSpecDatabase}'.");
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting JIRA Database Keyword extraction...");
        Console.WriteLine($"Using database: {_config.DbPath}");
        
        using SqliteConnection db = new SqliteConnection($"Data Source={_config.DbPath}");
        await db.OpenAsync();

        // drop existing tables if they exist
        DbIssueKeywordRecord.DropTable(db);
        DbCorpusKeywordRecord.DropTable(db);
        DbTotalFrequencyRecord.DropTable(db);
        
        // ensure our tables exist
        DbIssueKeywordRecord.CreateTable(db);
        DbCorpusKeywordRecord.CreateTable(db);
        DbTotalFrequencyRecord.CreateTable(db);

        // get current indexes
        DbIssueKeywordRecord.LoadMaxKey(db);
        DbCorpusKeywordRecord.LoadMaxKey(db);
        DbTotalFrequencyRecord.LoadMaxKey(db);
        
        processIssues(db);
    }


    private class IssueWordCountInfo : Dictionary<string, DbIssueKeywordRecord> { }
    private class CorpusWordCountInfo : Dictionary<string, DbCorpusKeywordRecord> { }

    private void processIssues(SqliteConnection db)
    {
        Console.WriteLine("Processing issues...");

        // get the list of issues
        List<IssueRecord> issues = IssueRecord.SelectList(db);
        Console.WriteLine($"  Found {issues.Count} issues.");

        Dictionary<int, IssueWordCountInfo> wordCountsByIssue = [];
        CorpusWordCountInfo totalWordCounts = [];

        Dictionary<int, DbTotalFrequencyRecord> totalFrequenciesByIssue = [];
        DbTotalFrequencyRecord totalFrequencies = new()
        {
            Id = DbTotalFrequencyRecord.GetIndex(),
            IssueId = null,
        };
        
        int issueIndex = 0;

        foreach (IssueRecord issue in issues)
        {
            issueIndex++;

            if (issueIndex % 100 == 0)
            {
                Console.WriteLine($"  Processing issue {issueIndex} of {issues.Count}, total unique words: {totalWordCounts.Count}...");

                // List<DbCorpusKeywordRecord> top10 = totalWordCounts.Values.OrderByDescending(c => c.Count).Take(10).ToList();
                // Console.WriteLine("  - Top 10 words:");
                // foreach (DbCorpusKeywordRecord ws in top10)
                // {
                //     Console.WriteLine($"    - Count: {ws.Count}: '{ws.Keyword}'");
                // }
            }

            // get any comments for this issue
            List<CommentRecord> comments = CommentRecord.SelectList(db, IssueId: issue.Id);

            // build the text to extract keywords from
            IssueWordCountInfo issueWordCounts = [];

            if (!string.IsNullOrWhiteSpace(issue.Title))
            {
                countFromString(
                    issue.Id, 
                    stripHtml(issue.Title), 
                    issueWordCounts, 
                    totalWordCounts,
                    totalFrequenciesByIssue,
                    totalFrequencies);
            }
            if (!string.IsNullOrWhiteSpace(issue.Description))
            {
                countFromString(
                    issue.Id, 
                    stripHtml(issue.Description), 
                    issueWordCounts, 
                    totalWordCounts,
                    totalFrequenciesByIssue,
                    totalFrequencies);
            }
            if (!string.IsNullOrWhiteSpace(issue.Summary))
            {
                countFromString(
                    issue.Id, 
                    stripHtml(issue.Summary), 
                    issueWordCounts, 
                    totalWordCounts,
                    totalFrequenciesByIssue,
                    totalFrequencies);
            }
            if (!string.IsNullOrWhiteSpace(issue.ResolutionDescription))
            {
                countFromString(
                    issue.Id, 
                    stripHtml(issue.ResolutionDescription),
                    issueWordCounts, 
                    totalWordCounts,
                    totalFrequenciesByIssue,
                    totalFrequencies);
            }

            foreach (CommentRecord comment in comments)
            {
                if (!string.IsNullOrWhiteSpace(comment.Body))
                {
                    countFromString(
                        issue.Id,
                        stripHtml(comment.Body), 
                        issueWordCounts, 
                        totalWordCounts,
                        totalFrequenciesByIssue,
                        totalFrequencies);
                }
            }

            wordCountsByIssue[issue.Id] = issueWordCounts;
        }

        // traverse issues to insert into the database
        foreach (IssueWordCountInfo issueWordCounts in wordCountsByIssue.Values)
        {
            // insert all issue words so we can do further analysis later
            issueWordCounts.Values.Insert(db);

            // only insert the most frequent keywords
            // issueWordCounts.Values.OrderByDescending(wc => wc.Count).Take(_keywordsPerIssue).Insert(db);
        }
        
        // insert all corpus keywords
        totalWordCounts.Values.Insert(db);
        
        // insert total frequencies
        totalFrequenciesByIssue.Values.Insert(db);
        totalFrequencies.Insert(db);
    }

    private void countFromString(
        int issueId,
        string input,
        IssueWordCountInfo issueWordCounts,
        CorpusWordCountInfo totalWordCounts,
        Dictionary<int, DbTotalFrequencyRecord> totalFrequenciesByIssue,
        DbTotalFrequencyRecord totalFrequencies)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }
        
        if (!totalFrequenciesByIssue.TryGetValue(issueId, out DbTotalFrequencyRecord? issueTotalFrequency))
        {
            issueTotalFrequency = new DbTotalFrequencyRecord()
            {
                Id = DbTotalFrequencyRecord.GetIndex(),
                IssueId = issueId,
            };
            totalFrequenciesByIssue[issueId] = issueTotalFrequency;
        }

        string[] parts = input.Split(_wordSplitChars, StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in parts)
        {
            (string sanitized, char firstLetter, char? prefixSymbol) = SanitizeAsKeyword(word);

            // do not process anything too short or only digits (all noise)
            if ((firstLetter == '\0') ||
                (sanitized.Length < _minKeywordLength))
            {
                continue;
            }

            issueTotalFrequency.TotalWords++;
            totalFrequencies.TotalWords++;

            // filter stop words first
            if (_stopWords.Contains(sanitized))
            {
                issueTotalFrequency.TotalStopWords++;
                totalFrequencies.TotalStopWords++;
                
                // do not track stop words - code is here for debugging
                // if (issueWordCounts.TryGetValue(sanitized, out WordStats swInfo))
                // {
                //     swInfo.Count++;
                // }
                // else
                // {
                //     issueWordCounts[sanitized] = new()
                //     {
                //         FirstOccurenceText = word,
                //         Sanitized = sanitized,
                //         Count = 1,
                //         KeywordType = KeywordTypeCodes.StopWord,
                //     };
                // }

                //if (totalWordCounts.TryGetValue(sanitized, out WordStats tswInfo))
                //{
                //    tswInfo.Count++;
                //}
                //else
                //{
                //    totalWordCounts[sanitized] = new()
                //    {
                //        FirstOccurenceText = word,
                //        Sanitized = sanitized,
                //        Count = 1,
                //        KeywordType = KeywordTypeCodes.StopWord,
                //    };
                //}

                continue;
            }
            
            bool isFhirElementPath = _fhirElementPaths.Contains(sanitized);
            bool isFhirOperationName = (prefixSymbol == '$') && _fhirOperationNames.Contains(sanitized);
            bool isLemma = _lemmas.TryGetValue(sanitized, out string? lemma);

            // process as a fhir element path if it is either not a lemma, or if it is a lemma and starts with an uppercase letter
            bool processAsFhirElementPath = isFhirElementPath &&
                (!isLemma || (isLemma && char.IsUpper(firstLetter)));
            
            // filter FHIR element paths next
            if (processAsFhirElementPath)
            {
                issueTotalFrequency.TotalFhirElementPaths++;
                totalFrequencies.TotalFhirElementPaths++;

                if (issueWordCounts.TryGetValue(sanitized, out DbIssueKeywordRecord? fwInfo))
                {
                    fwInfo.Count++;
                }
                else
                {
                    issueWordCounts[sanitized] = new()
                    {
                        Id = DbIssueKeywordRecord.GetIndex(),
                        IssueId = issueId,
                        Keyword = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirElementPath,
                    };
                }

                if (totalWordCounts.TryGetValue(sanitized, out DbCorpusKeywordRecord? tfwInfo))
                {
                    tfwInfo.Count++;
                }
                else
                {
                    totalWordCounts[sanitized] = new()
                    {
                        Id = DbCorpusKeywordRecord.GetIndex(),
                        Keyword = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirElementPath,
                    };
                }

                continue;
            }

            // check for a FHIR operation name
            if (isFhirOperationName)
            {
                issueTotalFrequency.TotalFhirOperationNames++;
                totalFrequencies.TotalFhirOperationNames++;

                if (issueWordCounts.TryGetValue(sanitized, out DbIssueKeywordRecord? owInfo))
                {
                    owInfo.Count++;
                }
                else
                {
                    issueWordCounts[sanitized] = new()
                    {
                        Id = DbIssueKeywordRecord.GetIndex(),
                        IssueId = issueId,
                        Keyword = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirOperationName,
                    };
                }
                if (totalWordCounts.TryGetValue(sanitized, out DbCorpusKeywordRecord? towInfo))
                {
                    towInfo.Count++;
                }
                else
                {
                    totalWordCounts[sanitized] = new()
                    {
                        Id = DbCorpusKeywordRecord.GetIndex(),
                        Keyword = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirOperationName,
                    };
                }
                continue;
            }

            // check for a lemma
            if (isLemma &&
                !string.IsNullOrWhiteSpace(lemma))
            {
                issueTotalFrequency.TotalLemmaWords++;
                totalFrequencies.TotalLemmaWords++;

                if (issueWordCounts.TryGetValue(lemma, out DbIssueKeywordRecord? lwInfo))
                {
                    lwInfo.Count++;
                }
                else
                {
                    issueWordCounts[lemma] = new()
                    {
                        Id = DbIssueKeywordRecord.GetIndex(),
                        IssueId = issueId,
                        Keyword = lemma,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.Word,
                    };
                }

                if (totalWordCounts.TryGetValue(lemma, out DbCorpusKeywordRecord? tlwInfo))
                {
                    tlwInfo.Count++;
                }
                else
                {
                    totalWordCounts[lemma] = new()
                    {
                        Id = DbCorpusKeywordRecord.GetIndex(),
                        Keyword = lemma,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.Word,
                    };
                }
                continue;
            }

            // finally, process as a normal word
            if (issueWordCounts.TryGetValue(sanitized, out DbIssueKeywordRecord? wInfo))
            {
                wInfo.Count++;
            }
            else
            {
                issueWordCounts[sanitized] = new()
                {
                    Id = DbIssueKeywordRecord.GetIndex(),
                    IssueId = issueId,
                    Keyword = sanitized,
                    Count = 1,
                    KeywordType = KeywordTypeCodes.Word,
                };
            }

            if (totalWordCounts.TryGetValue(sanitized, out DbCorpusKeywordRecord? twInfo))
            {
                twInfo.Count++;
            }
            else
            {
                totalWordCounts[sanitized] = new()
                {
                    Id = DbCorpusKeywordRecord.GetIndex(),
                    Keyword = sanitized,
                    Count = 1,
                    KeywordType = KeywordTypeCodes.Word,
                };
            }
        }
    }


    private FrozenDictionary<string, string> loadLemmas(string auxDbPath)
    {
        Dictionary<string, string> lemmas = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(auxDbPath) ||
            !File.Exists(auxDbPath))
        {
            Console.WriteLine($"Warning: Auxiliary database file '{auxDbPath}' does not exist. No lemmas will be used.");
            return lemmas.ToFrozenDictionary(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={auxDbPath}");
        db.Open();

        List<LemmaRecord> lemmaRecords = LemmaRecord.SelectList(db);
        foreach (LemmaRecord record in lemmaRecords)
        {
            // sanitize the keyword
            (string sanitized, char firstLetter, _) = SanitizeAsKeyword(record.Inflection);
            if ((firstLetter == '\0') ||
                (sanitized.Length < _minKeywordLength) ||
                lemmas.ContainsKey(sanitized))
            {
                continue;
            }

            // need to sanitize the lemma as well
            (string lemmaSanitized, _, _) = SanitizeAsKeyword(record.Lemma);
            lemmas[sanitized] = record.Lemma;
        }

        return lemmas.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private FrozenSet<string> loadStopWords(string auxDbPath)
    {
        HashSet<string> words = new(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(auxDbPath) ||
            !File.Exists(auxDbPath))
        {
            Console.WriteLine($"Warning: Auxiliary database file '{auxDbPath}' does not exist. No stop words will be used.");
            return words.ToFrozenSet(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={auxDbPath}");
        db.Open();

        {
            IDbCommand command = db.CreateCommand();
            command.CommandText = "SELECT DISTINCT word FROM stop_words WHERE word IS NOT NULL;";
            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                (string word, char firstLetter, _) = SanitizeAsKeyword(reader.GetString(0));
                if (firstLetter != '\0')
                {
                    words.Add(word);
                }
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

        // we don't parse based on version, so just get all paths
        {
            IDbCommand command = db.CreateCommand();
            command.CommandText = $"SELECT DISTINCT {nameof(CgDbElement.Path)} FROM {CgDbElement.DefaultTableName} WHERE {nameof(CgDbElement.Path)} IS NOT NULL;";
            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                (string path, char firstLetter, _) = SanitizeAsKeyword(reader.GetString(0));
                if (firstLetter != '\0')
                {
                    paths.Add(path);
                }
            }
        }

        {
            IDbCommand command = db.CreateCommand();
            command.CommandText = $"SELECT DISTINCT {nameof(CgDbOperation.Code)} FROM {CgDbOperation.DefaultTableName} WHERE {nameof(CgDbOperation.Code)} IS NOT NULL;";
            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                (string code, char firstLetter, _) = SanitizeAsKeyword(reader.GetString(0));
                if (firstLetter != '\0')
                {
                    operations.Add(code);
                }
            }
        }

        return (paths.ToFrozenSet(StringComparer.Ordinal), operations.ToFrozenSet(StringComparer.Ordinal));
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

    public static (string clean, char firstLetter, char? prefixSymbol) SanitizeAsKeyword(string? text)
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
