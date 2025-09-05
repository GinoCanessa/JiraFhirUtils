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

namespace jira_fhir_cli.Keyword;

public class KeywordProcessor
{
    private const int _minKeywordLength = 3;

    private enum KeywordTypeCodes : int
    {
        Word = 0,
        StopWord = 1,
        FhirElementPath = 2,
        FhirOperationName = 3,
    }
    private record class WordStats
    {
        public required string FirstOccurenceText { get; set; }
        public required string Sanitized { get; set; }
        public required int Count { get; set; }
        public required KeywordTypeCodes KeywordType { get; set; }
    }

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

        processIssues(db);
    }


    private class BlockWordCountInfo : Dictionary<string, WordStats> { }

    private void processIssues(SqliteConnection db)
    {
        Console.WriteLine("Processing issues...");

        // get the list of issues
        List<IssueRecord> issues = IssueRecord.SelectList(db);
        Console.WriteLine($"  Found {issues.Count} issues.");

        Dictionary<int, BlockWordCountInfo> wordCountsByIssue = [];
        BlockWordCountInfo totalWordCounts = [];

        int issueIndex = 0;

        foreach (IssueRecord issue in issues)
        {
            issueIndex++;

            if (issueIndex % 100 == 0)
            {
                Console.WriteLine($"  Processing issue {issueIndex} of {issues.Count}, total unique words: {totalWordCounts.Count}...");

                List<WordStats> top10 = totalWordCounts.Values.OrderByDescending(c => c.Count).Take(10).ToList();
                Console.WriteLine("  - Top 10 words:");
                foreach (WordStats ws in top10)
                {
                    Console.WriteLine($"    - Count: {ws.Count}: '{ws.Sanitized}'");
                }
            }

            // get any comments for this issue
            List<CommentRecord> comments = CommentRecord.SelectList(db, IssueId: issue.Id);

            // build the text to extract keywords from
            BlockWordCountInfo issueWordCounts = [];

            if (!string.IsNullOrWhiteSpace(issue.Title))
            {
                countFromString(stripHtml(issue.Title), issueWordCounts, totalWordCounts);
            }
            if (!string.IsNullOrWhiteSpace(issue.Description))
            {
                countFromString(stripHtml(issue.Description), issueWordCounts, totalWordCounts);
            }
            if (!string.IsNullOrWhiteSpace(issue.Summary))
            {
                countFromString(stripHtml(issue.Summary), issueWordCounts, totalWordCounts);
            }
            if (!string.IsNullOrWhiteSpace(issue.ResolutionDescription))
            {
                countFromString(stripHtml(issue.ResolutionDescription), issueWordCounts, totalWordCounts);
            }

            foreach (CommentRecord comment in comments)
            {
                if (!string.IsNullOrWhiteSpace(comment.Body))
                {
                    countFromString(stripHtml(comment.Body), issueWordCounts, totalWordCounts);
                }
            }

            wordCountsByIssue[issue.Id] = issueWordCounts;
        }

        return;
    }

    private void countFromString(
        string input,
        BlockWordCountInfo issueWordCounts,
        BlockWordCountInfo totalWordCounts)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        string[] parts = input.Split(_wordSplitChars, StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in parts)
        {
            (string sanitized, bool hasAlpha) = SanitizeAsKeyword(word);

            // do not process anything too short or only digits (all noise)
            if (!hasAlpha ||
                (sanitized.Length < _minKeywordLength))
            {
                continue;
            }

            // filter stop words first
            if (_stopWords.Contains(sanitized))
            {
                // do not track stop words - code is here for debugging
                //if (issueWordCounts.TryGetValue(sanitized, out WordStats swInfo))
                //{
                //    swInfo.Count++;
                //}
                //else
                //{
                //    issueWordCounts[sanitized] = new()
                //    {
                //        FirstOccurenceText = word,
                //        Sanitized = sanitized,
                //        Count = 1,
                //        KeywordType = KeywordTypeCodes.StopWord,
                //    };
                //}

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

            // filter FHIR element paths next
            if (_fhirElementPaths.Contains(sanitized))
            {
                if (issueWordCounts.TryGetValue(sanitized, out WordStats? fwInfo))
                {
                    fwInfo.Count++;
                }
                else
                {
                    issueWordCounts[sanitized] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirElementPath,
                    };
                }

                if (totalWordCounts.TryGetValue(sanitized, out WordStats? tfwInfo))
                {
                    tfwInfo.Count++;
                }
                else
                {
                    totalWordCounts[sanitized] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirElementPath,
                    };
                }

                continue;
            }

            // check for a FHIR operation name
            if ((word[0] == '$') &&
                _fhirOperationNames.Contains(sanitized))
            {
                if (issueWordCounts.TryGetValue(sanitized, out WordStats? owInfo))
                {
                    owInfo.Count++;
                }
                else
                {
                    issueWordCounts[sanitized] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirOperationName,
                    };
                }
                if (totalWordCounts.TryGetValue(sanitized, out WordStats? towInfo))
                {
                    towInfo.Count++;
                }
                else
                {
                    totalWordCounts[sanitized] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = sanitized,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.FhirOperationName,
                    };
                }
                continue;
            }

            // check for a lemma
            if (_lemmas.TryGetValue(sanitized, out string? lemma))
            {
                if (issueWordCounts.TryGetValue(lemma, out WordStats? lwInfo))
                {
                    lwInfo.Count++;
                }
                else
                {
                    issueWordCounts[lemma] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = lemma,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.Word,
                    };
                }

                if (totalWordCounts.TryGetValue(lemma, out WordStats? tlwInfo))
                {
                    tlwInfo.Count++;
                }
                else
                {
                    totalWordCounts[lemma] = new()
                    {
                        FirstOccurenceText = word,
                        Sanitized = lemma,
                        Count = 1,
                        KeywordType = KeywordTypeCodes.Word,
                    };
                }
                continue;
            }

            // finally, process as a normal word
            if (issueWordCounts.TryGetValue(sanitized, out WordStats? wInfo))
            {
                wInfo.Count++;
            }
            else
            {
                issueWordCounts[sanitized] = new()
                {
                    FirstOccurenceText = word,
                    Sanitized = sanitized,
                    Count = 1,
                    KeywordType = KeywordTypeCodes.Word,
                };
            }

            if (totalWordCounts.TryGetValue(sanitized, out WordStats? twInfo))
            {
                twInfo.Count++;
            }
            else
            {
                totalWordCounts[sanitized] = new()
                {
                    FirstOccurenceText = word,
                    Sanitized = sanitized,
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
            Console.WriteLine($"Warning: Auxilary database file '{auxDbPath}' does not exist. No lemmas will be used.");
            return lemmas.ToFrozenDictionary(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={auxDbPath}");
        db.Open();

        List<LemmaRecord> lemmaRecords = LemmaRecord.SelectList(db);
        foreach (LemmaRecord record in lemmaRecords)
        {
            // sanitize the keyword
            (string sanitized, bool hasAlpha) = SanitizeAsKeyword(record.Inflection);
            if ((!hasAlpha) ||
                (sanitized.Length < _minKeywordLength) ||
                lemmas.ContainsKey(sanitized))
            {
                continue;
            }

            // need to sanitize the lemma as well
            (string lemmaSanitized, _) = SanitizeAsKeyword(record.Lemma);
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
            Console.WriteLine($"Warning: Auxilary database file '{auxDbPath}' does not exist. No stopwords will be used.");
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
                (string word, bool hasAlpha) = SanitizeAsKeyword(reader.GetString(0));
                if (hasAlpha)
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
            command.CommandText = "SELECT DISTINCT Path FROM Elements WHERE Path IS NOT NULL;";
            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                (string path, bool hasAlpha) = SanitizeAsKeyword(reader.GetString(0));
                if (hasAlpha)
                {
                    paths.Add(path);
                }
            }
        }

        {
            IDbCommand command = db.CreateCommand();
            command.CommandText = "SELECT DISTINCT Code FROM Operations WHERE Code IS NOT NULL;";
            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                (string code, bool hasAlpha) = SanitizeAsKeyword(reader.GetString(0));
                if (hasAlpha)
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

    public static (string clean, bool hasAlpha) SanitizeAsKeyword(string? text)
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
