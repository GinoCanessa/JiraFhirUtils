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
            double? idf = DbCorpusKeywordRecord.SelectSingle(db, Keyword: issueKeyword.Keyword, KeywordType: issueKeyword.KeywordType)?.Idf;
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
        int issueCount;
        {
            using IDbCommand command = db.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {DbTotalFrequencyRecord.DefaultTableName} WHERE IssueId IS NOT NULL;";
            
            object? result = command.ExecuteScalar();
            issueCount = result != null ? Convert.ToInt32(result) : 0;

            if (issueCount == 0)
            {
                return 0.0;
            }
        }
        
        int wordCount;
        {
            using IDbCommand command = db.CreateCommand();
            command.CommandText = $"SELECT SUM(TotalWords) FROM {DbTotalFrequencyRecord.DefaultTableName} WHERE IssueId IS NOT NULL;";
            
            object? result = command.ExecuteScalar();
            wordCount = result != null ? Convert.ToInt32(result) : 0;

            if (wordCount == 0)
            {
                return 0.0;
            }
        }

        return (wordCount * 1.0d) / (issueCount * 1.0d);
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

    /// <summary>
    /// Updates IDF values for existing corpus keywords without dropping tables
    /// </summary>
    public void UpdateCorpusIdf(SqliteConnection db, Action<string>? progressCallback = null)
    {
        progressCallback?.Invoke("Starting IDF update for corpus keywords...");

        // Validate that required data exists
        if (!DbCorpusKeywordRecord.ValidateCorpusKeywordsExist(db))
        {
            throw new InvalidOperationException("No corpus keywords found. Cannot update IDF values.");
        }

        int totalDocuments = GetTotalDocumentCount(db);
        progressCallback?.Invoke($"Total documents: {totalDocuments}");

        if (totalDocuments <= 0)
        {
            throw new InvalidOperationException("No documents found. Cannot calculate IDF values.");
        }

        int keywordCount = DbCorpusKeywordRecord.SelectCount(db);
        progressCallback?.Invoke($"Updating IDF for {keywordCount} corpus keywords...");

        const int batchSize = 5000;
        int processed = 0;

        for (int i = 0; i < keywordCount; i += batchSize)
        {
            List<DbCorpusKeywordRecord> batch = DbCorpusKeywordRecord.SelectList(
                db,
                resultLimit: batchSize,
                resultOffset: i,
                orderByProperties: [nameof(DbCorpusKeywordRecord.Id)]);

            // Calculate IDF for each keyword in the batch
            foreach (DbCorpusKeywordRecord keyword in batch)
            {
                int documentsContainingTerm = GetDocumentCountForKeyword(db, keyword.Keyword, keyword.KeywordType);
                keyword.Idf = CalculateIdf(totalDocuments, documentsContainingTerm);
                processed++;
            }

            // Update the batch
            batch.Update(db);

            progressCallback?.Invoke($"Processed {processed}/{keywordCount} keywords ({(double)processed / keywordCount * 100:F1}%)");
        }

        progressCallback?.Invoke("IDF update completed successfully.");
    }

    /// <summary>
    /// Updates BM25 scores for existing issue keywords without dropping tables
    /// </summary>
    public void UpdateIssueBm25Scores(SqliteConnection db, Action<string>? progressCallback = null)
    {
        progressCallback?.Invoke("Starting BM25 score update for issue keywords...");

        // Validate that required data exists
        if (!DbIssueKeywordRecord.ValidateIssueKeywordsExist(db))
        {
            throw new InvalidOperationException("No issue keywords found. Cannot update BM25 scores.");
        }

        if (!DbCorpusKeywordRecord.ValidateCorpusKeywordsExist(db))
        {
            throw new InvalidOperationException("No corpus keywords found. Cannot calculate BM25 scores without IDF values.");
        }

        double averageDocumentLength = CalculateAverageDocumentLength(db);
        progressCallback?.Invoke($"Average document length: {averageDocumentLength:F2}");

        if (averageDocumentLength <= 0)
        {
            throw new InvalidOperationException("Average document length is 0. Cannot calculate BM25 scores.");
        }

        List<IssueRecord> issues = IssueRecord.SelectList(db);
        progressCallback?.Invoke($"Processing BM25 scores for {issues.Count} issues...");

        int processedIssues = 0;
        var bm25Updates = new Dictionary<(int issueId, string keyword, KeywordTypeCodes keywordType), double>();

        using (SqliteTransaction transaction = db.BeginTransaction())
        {
            try
            {
                foreach (IssueRecord issue in issues)
                {
                    ProcessIssueKeywordsForUpdate(db, issue.Id, averageDocumentLength, bm25Updates);
                    processedIssues++;

                    // Process in batches to avoid memory issues
                    if (bm25Updates.Count >= 500)
                    {
                        DbIssueKeywordRecord.UpdateBm25ScoreBulk(db, bm25Updates, transaction);
                        bm25Updates.Clear();
                    }

                    if (processedIssues % 100 == 0)
                    {
                        progressCallback?.Invoke($"Processing issue {processedIssues}/{issues.Count} ({(double)processedIssues / issues.Count * 100:F1}%)");
                    }
                }

                // Process any remaining updates
                if (bm25Updates.Count > 0)
                {
                    DbIssueKeywordRecord.UpdateBm25ScoreBulk(db, bm25Updates, transaction);
                }

                transaction.Commit();
                progressCallback?.Invoke("BM25 score update completed successfully.");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <summary>
    /// Helper method for processing issue keywords and collecting BM25 score updates
    /// </summary>
    private void ProcessIssueKeywordsForUpdate(SqliteConnection db, int issueId, double averageDocumentLength, Dictionary<(int, string, KeywordTypeCodes), double> bm25Updates)
    {
        List<DbIssueKeywordRecord> issueKeywords = DbIssueKeywordRecord.SelectList(db, IssueId: issueId);

        DbTotalFrequencyRecord? issueStats = DbTotalFrequencyRecord.SelectList(db, IssueId: issueId).FirstOrDefault();
        if (issueStats == null)
        {
            return; // Skip issues without frequency stats
        }

        int documentLength = issueStats.TotalWords;

        foreach (DbIssueKeywordRecord issueKeyword in issueKeywords)
        {
            double? idf = DbCorpusKeywordRecord.SelectSingle(db, Keyword: issueKeyword.Keyword, KeywordType: issueKeyword.KeywordType)?.Idf;
            if (idf.HasValue)
            {
                double bm25Score = CalculateBm25Score(
                    issueKeyword.Count,
                    idf.Value,
                    documentLength,
                    averageDocumentLength);

                bm25Updates[(issueId, issueKeyword.Keyword, issueKeyword.KeywordType)] = bm25Score;
            }
        }
    }

    /// <summary>
    /// Main entry point for recalculating all IDF and BM25 scores using existing frequency data
    /// </summary>
    public void RecalculateAllScores(SqliteConnection db, Action<string>? progressCallback = null)
    {
        progressCallback?.Invoke("Starting complete score recalculation...");

        // Validate that frequency tables exist
        progressCallback?.Invoke("Validating required data exists...");

        if (!DbCorpusKeywordRecord.ValidateCorpusKeywordsExist(db))
        {
            throw new InvalidOperationException("Corpus keywords table is empty. Run extract-keywords first to populate frequency data.");
        }

        if (!DbIssueKeywordRecord.ValidateIssueKeywordsExist(db))
        {
            throw new InvalidOperationException("Issue keywords table is empty. Run extract-keywords first to populate frequency data.");
        }

        // Step 1: Update IDF values
        progressCallback?.Invoke("Phase 1/3: Updating IDF values...");
        UpdateCorpusIdf(db, progressCallback);

        // Step 2: Update BM25 scores
        progressCallback?.Invoke("Phase 2/3: Updating BM25 scores...");
        UpdateIssueBm25Scores(db, progressCallback);

        // Step 3: Update statistics and config tables
        progressCallback?.Invoke("Phase 3/3: Updating statistics and configuration...");
        UpdateDocumentStats(db);
        UpdateBm25Config(db);

        progressCallback?.Invoke("Score recalculation completed successfully!");
    }

    /// <summary>
    /// Updates document stats without dropping the table
    /// </summary>
    private void UpdateDocumentStats(SqliteConnection db)
    {
        double avgDocLength = CalculateAverageDocumentLength(db);
        int totalDocs = GetTotalDocumentCount(db);

        // Check if table exists and has data
        List<DbDocumentStatsRecord> existingStats = DbDocumentStatsRecord.SelectList(db);

        if (existingStats.Count > 0)
        {
            // Update existing record
            var stats = existingStats.First();
            stats.AverageDocumentLength = avgDocLength;
            stats.TotalDocumentCount = totalDocs;
            stats.LastCalculated = DateTime.UtcNow;
            stats.Update(db);
        }
        else
        {
            // Create new record
            DbDocumentStatsRecord.LoadMaxKey(db);
            DbDocumentStatsRecord stats = new()
            {
                Id = DbDocumentStatsRecord.GetIndex(),
                AverageDocumentLength = avgDocLength,
                TotalDocumentCount = totalDocs,
                LastCalculated = DateTime.UtcNow
            };
            stats.Insert(db);
        }
    }

    /// <summary>
    /// Updates BM25 config without dropping the table
    /// </summary>
    private void UpdateBm25Config(SqliteConnection db)
    {
        // Check if table exists and has data
        List<DbBm25ConfigRecord> existingConfigs = DbBm25ConfigRecord.SelectList(db);

        if (existingConfigs.Count > 0)
        {
            // Update existing record
            var config = existingConfigs.OrderByDescending(c => c.LastUpdated).First();
            config.K1 = _k1;
            config.B = _b;
            config.LastUpdated = DateTime.UtcNow;
            config.Update(db);
        }
        else
        {
            // Create new record
            DbBm25ConfigRecord.LoadMaxKey(db);
            DbBm25ConfigRecord config = new()
            {
                Id = DbBm25ConfigRecord.GetIndex(),
                K1 = _k1,
                B = _b,
                LastUpdated = DateTime.UtcNow
            };
            config.Insert(db);
        }
    }
}