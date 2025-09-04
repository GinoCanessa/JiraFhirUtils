using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace jira_fhir_cli.Keyword;

public class KeywordProcessor
{
    private const int _minKeywordLength = 3;

    private static System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly char[] _wordSplitChars = [' ', '\t', '\r', '\n'];

    private CliConfig _config;
    private FrozenSet<string> _stopWords;
    private FrozenSet<string> _fhirElementPaths;

    public KeywordProcessor(CliConfig config)
    {
        _config = config;

        _stopWords = loadStopWords(config.StopwordFile);
        Console.WriteLine($"Loaded {_stopWords.Count} stopwords from '{config.StopwordFile}'.");

        _fhirElementPaths = loadFhirSpecContent(config.FhirSpecDatabase);
        Console.WriteLine($"Loaded {_fhirElementPaths.Count} FHIR element paths from '{config.FhirSpecDatabase}'.");
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting JIRA Database Keyword extraction...");
        Console.WriteLine($"Using database: {_config.DbPath}");

        using SqliteConnection db = new SqliteConnection($"Data Source={_config.DbPath}");
        await db.OpenAsync();

        processIssues(db);
    }

    private record struct WordStats(
        string FirstOccurrence,
        string Sanitized,
        int Count,
        bool IsStopWord,
        bool IsFhirWord);

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
        foreach (string part in parts)
        {
            string word = SanitizeAsKeyword(part);
            if (word.Length < _minKeywordLength)
            {
                continue;
            }

            if (_stopWords.Contains(word))
            {
                if (issueWordCounts.TryGetValue(word, out WordStats swInfo))
                {
                    swInfo.Count++;
                }
                else
                {
                    issueWordCounts[word] = new()
                    {
                        FirstOccurrence = part,
                        Sanitized = word,
                        Count = 1,
                        IsStopWord = true,
                        IsFhirWord = false
                    };
                }

                if (totalWordCounts.TryGetValue(word, out WordStats tswInfo))
                {
                    tswInfo.Count++;
                }
                else
                {
                    totalWordCounts[word] = new()
                    {
                        FirstOccurrence = part,
                        Sanitized = word,
                        Count = 1,
                        IsStopWord = true,
                        IsFhirWord = false
                    };
                }

                continue;
            }

            if (_fhirElementPaths.Contains(word))
            {
                if (issueWordCounts.TryGetValue(word, out WordStats fwInfo))
                {
                    fwInfo.Count++;
                }
                else
                {
                    issueWordCounts[word] = new()
                    {
                        FirstOccurrence = part,
                        Sanitized = word,
                        Count = 1,
                        IsStopWord = false,
                        IsFhirWord = true
                    };
                }

                if (totalWordCounts.TryGetValue(word, out WordStats tfwInfo))
                {
                    tfwInfo.Count++;
                }
                else
                {
                    totalWordCounts[word] = new()
                    {
                        FirstOccurrence = part,
                        Sanitized = word,
                        Count = 1,
                        IsStopWord = false,
                        IsFhirWord = true
                    };
                }

                continue;
            }

            if (issueWordCounts.TryGetValue(word, out WordStats wInfo))
            {
                wInfo.Count++;
            }
            else
            {
                issueWordCounts[word] = new()
                {
                    FirstOccurrence = part,
                    Sanitized = word,
                    Count = 1,
                    IsStopWord = false,
                    IsFhirWord = false
                };
            }

            if (totalWordCounts.TryGetValue(word, out WordStats twInfo))
            {
                twInfo.Count++;
            }
            else
            {
                totalWordCounts[word] = new()
                {
                    Count = 1,
                    IsStopWord = false,
                    IsFhirWord = false
                };
            }
        }
    }

    private FrozenSet<string> loadStopWords(string path)
    {
        HashSet<string> words = new(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            Console.WriteLine($"Warning: Stopword file '{path}' does not exist. No stopwords will be used.");
            return words.ToFrozenSet(StringComparer.Ordinal);
        }

        foreach (string line in File.ReadLines(path))
        {
            string word = SanitizeAsKeyword(line);

            if (!string.IsNullOrEmpty(word) &&
                (word.Length >= _minKeywordLength) &&
                !line.StartsWith('#'))
            {
                words.Add(word);
            }
        }
        return words.ToFrozenSet(StringComparer.Ordinal);
    }

    private FrozenSet<string> loadFhirSpecContent(string? fhirSpecDbPath)
    {
        HashSet<string> paths = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(fhirSpecDbPath) || !File.Exists(fhirSpecDbPath))
        {
            Console.WriteLine($"Warning: FHIR spec database file '{fhirSpecDbPath}' does not exist. No FHIR element paths will be used.");
            return paths.ToFrozenSet(StringComparer.Ordinal);
        }

        using SqliteConnection db = new SqliteConnection($"Data Source={fhirSpecDbPath}");
        db.Open();

        // we don't parse based on version, so just get all paths
        IDbCommand command = db.CreateCommand();
        command.CommandText = "SELECT DISTINCT Path FROM Elements WHERE Path IS NOT NULL;";
        using IDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string path = SanitizeAsKeyword(reader.GetString(0));
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        return paths.ToFrozenSet(StringComparer.Ordinal);
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

    public static string SanitizeAsKeyword(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);

            switch (uc)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    sb.Append(char.ToLower(c));
                    break;

                case UnicodeCategory.LowercaseLetter:
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
        return sb.ToString();
    }
}
