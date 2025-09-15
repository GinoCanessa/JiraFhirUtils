using JiraFhirUtils.Common;
using JiraFhirUtils.Common.FhirDbModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace jira_fhir_cli.Keyword;

public class Bm25Calculator
{
    private readonly double _k1;
    private readonly double _b;
    
    public Bm25Calculator(double k1 = 1.2, double b = 0.75)
    {
        _k1 = k1;
        _b = b;
    }

    public double CalculateIdf(int totalDocuments, int documentsContainingTerm)
    {
        if (documentsContainingTerm <= 0 || totalDocuments <= 0)
        {
            return 0.0;
        }
        
        return Math.Log((totalDocuments - documentsContainingTerm + 0.5) / (documentsContainingTerm + 0.5));
    }

    public double CalculateBm25Score(
        double termFrequency,
        double idf,
        int documentLength,
        double averageDocumentLength)
    {
        if (termFrequency <= 0 || averageDocumentLength <= 0)
        {
            return 0.0;
        }

        double normalizedTermFrequency = termFrequency * (_k1 + 1) / 
            (termFrequency + _k1 * (1 - _b + _b * documentLength / averageDocumentLength));
        
        return idf * normalizedTermFrequency;
    }

    public void CalculateCorpusIdf(SqliteConnection db)
    {
        Console.WriteLine("Calculating IDF values for corpus keywords...");
        
        int totalDocuments = GetTotalDocumentCount(db);
        Console.WriteLine($"Total documents: {totalDocuments}");

        if (totalDocuments <= 0)
        {
            Console.WriteLine("Warning: No documents found. Cannot calculate IDF values.");
            return;
        }

        List<DbCorpusKeywordRecord> corpusKeywords = DbCorpusKeywordRecord.SelectList(db);
        Console.WriteLine($"Calculating IDF for {corpusKeywords.Count} corpus keywords...");

        int processed = 0;
        foreach (DbCorpusKeywordRecord keyword in corpusKeywords)
        {
            processed++;
            if (processed % 1000 == 0)
            {
                Console.WriteLine($"  Processed {processed}/{corpusKeywords.Count} keywords...");
            }

            int documentsContainingTerm = GetDocumentCountForKeyword(db, keyword.Keyword, keyword.KeywordType);
            keyword.Idf = CalculateIdf(totalDocuments, documentsContainingTerm);
        }

        Console.WriteLine("Updating corpus keywords with IDF values...");
        corpusKeywords.Update(db);
        Console.WriteLine("IDF calculation completed.");
    }

    public void CalculateDocumentBm25Scores(SqliteConnection db)
    {
        Console.WriteLine("Calculating BM25 scores for all issue keywords...");
        
        double averageDocumentLength = CalculateAverageDocumentLength(db);
        Console.WriteLine($"Average document length: {averageDocumentLength:F2}");

        if (averageDocumentLength <= 0)
        {
            Console.WriteLine("Warning: Average document length is 0. Cannot calculate BM25 scores.");
            return;
        }

        List<IssueRecord> issues = IssueRecord.SelectList(db);
        Console.WriteLine($"Processing BM25 scores for {issues.Count} issues...");

        int processedIssues = 0;
        foreach (IssueRecord issue in issues)
        {
            processedIssues++;
            if (processedIssues % 100 == 0)
            {
                Console.WriteLine($"  Processing issue {processedIssues}/{issues.Count}...");
            }

            ProcessIssueKeywords(db, issue.Id, averageDocumentLength);
        }

        Console.WriteLine("BM25 score calculation completed.");
    }

    private void ProcessIssueKeywords(SqliteConnection db, int issueId, double averageDocumentLength)
    {
        List<DbIssueKeywordRecord> issueKeywords = DbIssueKeywordRecord.SelectList(db, IssueId: issueId);
        
        DbTotalFrequencyRecord? issueStats = DbTotalFrequencyRecord.SelectList(db, IssueId: issueId).FirstOrDefault();
        if (issueStats == null)
        {
            Console.WriteLine($"Warning: No frequency stats found for issue {issueId}");
            return;
        }

        int documentLength = issueStats.TotalWords;
        
        foreach (DbIssueKeywordRecord issueKeyword in issueKeywords)
        {
            double? idf = GetIdfForKeyword(db, issueKeyword.Keyword, issueKeyword.KeywordType);
            if (idf.HasValue)
            {
                issueKeyword.Bm25Score = CalculateBm25Score(
                    issueKeyword.Count,
                    idf.Value,
                    documentLength,
                    averageDocumentLength);
            }
            else
            {
                issueKeyword.Bm25Score = 0.0;
            }
        }

        issueKeywords.Update(db);
    }

    public double CalculateAverageDocumentLength(SqliteConnection db)
    {
        List<DbTotalFrequencyRecord> issueStats = DbTotalFrequencyRecord.SelectList(db)
            .Where(tf => tf.IssueId.HasValue && tf.TotalWords > 0)
            .ToList();

        if (issueStats.Count == 0)
        {
            return 0.0;
        }

        double totalWords = issueStats.Sum(stats => stats.TotalWords);
        return totalWords / issueStats.Count;
    }

    private int GetTotalDocumentCount(SqliteConnection db)
    {
        using IDbCommand command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM total_frequencies WHERE IssueId IS NOT NULL;";
        
        object? result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private int GetDocumentCountForKeyword(SqliteConnection db, string keyword, KeywordTypeCodes keywordType)
    {
        using IDbCommand command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(DISTINCT IssueId) FROM issue_keywords WHERE Keyword = @keyword AND KeywordType = @keywordType;";

        var keywordParam = command.CreateParameter();
        keywordParam.ParameterName = "@keyword";
        keywordParam.Value = keyword;
        command.Parameters.Add(keywordParam);

        var typeParam = command.CreateParameter();
        typeParam.ParameterName = "@keywordType";
        typeParam.Value = keywordType.ToString();
        command.Parameters.Add(typeParam);

        object? result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private double? GetIdfForKeyword(SqliteConnection db, string keyword, KeywordTypeCodes keywordType)
    {
        using IDbCommand command = db.CreateCommand();
        command.CommandText = "SELECT Idf FROM corpus_keywords WHERE Keyword = @keyword AND KeywordType = @keywordType;";

        var keywordParam = command.CreateParameter();
        keywordParam.ParameterName = "@keyword";
        keywordParam.Value = keyword;
        command.Parameters.Add(keywordParam);

        var typeParam = command.CreateParameter();
        typeParam.ParameterName = "@keywordType";
        typeParam.Value = keywordType.ToString();
        command.Parameters.Add(typeParam);

        object? result = command.ExecuteScalar();
        return result != DBNull.Value && result != null ? Convert.ToDouble(result) : null;
    }

    public void StoreDocumentStats(SqliteConnection db)
    {
        Console.WriteLine("Storing document statistics...");
        
        DbDocumentStatsRecord.DropTable(db);
        DbDocumentStatsRecord.CreateTable(db);
        DbDocumentStatsRecord.LoadMaxKey(db);

        double avgDocLength = CalculateAverageDocumentLength(db);
        int totalDocs = GetTotalDocumentCount(db);

        DbDocumentStatsRecord stats = new()
        {
            Id = DbDocumentStatsRecord.GetIndex(),
            AverageDocumentLength = avgDocLength,
            TotalDocumentCount = totalDocs,
            LastCalculated = DateTime.UtcNow
        };

        stats.Insert(db);
        Console.WriteLine($"Stored document stats: {totalDocs} documents, avg length: {avgDocLength:F2}");
    }

    public void StoreBm25Config(SqliteConnection db)
    {
        Console.WriteLine("Storing BM25 configuration...");
        
        DbBm25ConfigRecord.DropTable(db);
        DbBm25ConfigRecord.CreateTable(db);
        DbBm25ConfigRecord.LoadMaxKey(db);

        DbBm25ConfigRecord config = new()
        {
            Id = DbBm25ConfigRecord.GetIndex(),
            K1 = _k1,
            B = _b,
            LastUpdated = DateTime.UtcNow
        };

        config.Insert(db);
        Console.WriteLine($"Stored BM25 config: k1={_k1}, b={_b}");
    }

    public static (double k1, double b) LoadBm25Config(SqliteConnection db)
    {
        List<DbBm25ConfigRecord> configs = DbBm25ConfigRecord.SelectList(db);
        
        if (configs.Count > 0)
        {
            DbBm25ConfigRecord config = configs.OrderByDescending(c => c.LastUpdated).First();
            return (config.K1, config.B);
        }

        return (1.2, 0.75);
    }
}