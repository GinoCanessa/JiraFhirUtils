using Microsoft.Data.Sqlite;
using JiraFhirUtils.Common;
using jira_fhir_cli.LlmProvider;

namespace jira_fhir_cli.Summary;

public class AiSummaryProcessor
{
    private readonly CliConfig _config;
    private readonly ISemanticKernelService _llmService;

    private static System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

    public AiSummaryProcessor(CliConfig config)
    {
        _config = config;
        _llmService = SemanticKernelServiceFactory.CreateService(_config);
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting AI Summarization process...");
        Console.WriteLine($"Using provider: {_llmService.ProviderName}");
        Console.WriteLine($"Using database: {_config.DbPath}");
        
        // Test connection first
        Console.WriteLine("Testing LLM connection...");
        if (!await _llmService.ValidateConnectionAsync())
        {
            throw new Exception("Failed to connect to LLM provider. Please check your configuration.");
        }
        Console.WriteLine("LLM connection successful!");

        using SqliteConnection connection = new($"Data Source={_config.DbPath}");
        await connection.OpenAsync();

        // Get issues that need summaries
        List<IssueRecord> issuesToProcess = getIssuesNeedingSummaries(connection);
        Console.WriteLine($"Found {issuesToProcess.Count} issues to process");

        if (issuesToProcess.Count == 0)
        {
            Console.WriteLine("No issues need summarization. Use --overwrite to regenerate existing summaries.");
            return;
        }

        // Process in batches
        for (int i = 0; i < issuesToProcess.Count; i += _config.BatchSize)
        {
            List<IssueRecord> batch = issuesToProcess.Skip(i).Take(_config.BatchSize).ToList();
            await processBatch(connection, batch);
            
            Console.WriteLine($"Processed {Math.Min(i + _config.BatchSize, issuesToProcess.Count)}/{issuesToProcess.Count} issues");
        }
        
        Console.WriteLine("AI Summarization complete!");
    }

    private List<IssueRecord> getIssuesNeedingSummaries(SqliteConnection db)
    {
        List<IssueRecord> issues = _config.OverwriteSummaries
            ? IssueRecord.SelectList(db)
            : IssueRecord.SelectList(
                db,
                orderByProperties: [nameof(IssueRecord.Id)],
                orderByDirection: "DESC",
                orJoinConditions: true,
                AiIssueSummaryIsNull: true,
                AiCommentSummaryIsNull: true);

        if (_config.OverwriteSummaries)
        {
            return issues;
        }
        
        // select any issues that were not selected above, have a resolution, and need a resolution summary
        List<IssueRecord> issuesNeedingResolutionSummary = IssueRecord.SelectList(
            db,
            orderByProperties: [nameof(IssueRecord.Id)],
            orderByDirection: "DESC",
            AiIssueSummaryIsNull: false,
            AiCommentSummaryIsNull: false,
            AiResolutionSummaryIsNull: true,
            ResolutionDescriptionIsNull: false);
        
        issues.AddRange(issuesNeedingResolutionSummary);
        
        return issues;
    }

    private async Task processBatch(SqliteConnection db, List<IssueRecord> issues)
    {
        foreach (IssueRecord issue in issues)
        {
            try
            {
                Console.WriteLine($"Processing issue {issue.Key}...");
                await processSingleIssue(db, issue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing issue {issue.Key}: {ex.Message}");
                // Continue with next issue
            }
        }
    }

    private async Task processSingleIssue(SqliteConnection db, IssueRecord issue)
    {
        bool updateIssue = false;

        // Generate issue summary
        if ((issue.AiIssueSummary == null || _config.OverwriteSummaries))
        {
            string? issueSummary = await generateIssueSummary(issue);
            if (issueSummary != null)
            {
                issue.AiIssueSummary = issueSummary;
                updateIssue = true;
                // Console.WriteLine($"  Generated issue summary for {issue.Key}");
            }
        }

        // Generate comment summary
        if ((issue.AiCommentSummary == null || _config.OverwriteSummaries))
        {
            string? commentSummary = await generateCommentSummary(db, issue.Id);
            if (commentSummary != null)
            {
                issue.AiCommentSummary = commentSummary;
                updateIssue = true;
                // Console.WriteLine($"  Generated comment summary for {issue.Key}");
            }
        }

        // Generate resolution summary
        if ((issue.AiResolutionSummary == null || _config.OverwriteSummaries))
        {
            string? resolutionSummary = await generateResolutionSummary(issue);
            if (resolutionSummary != null)
            {
                issue.AiResolutionSummary = resolutionSummary;
                updateIssue = true;
                // Console.WriteLine($"  Generated resolution summary for {issue.Key}");
            }
        }

        // Update database if we have any summaries
        if (updateIssue)
        {
            issue.Update(db);
        }
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

    private async Task<string?> generateIssueSummary(IssueRecord issue)
    {
        if (string.IsNullOrEmpty(issue.Title) && string.IsNullOrEmpty(issue.Description))
        {
            return "No issue content to summarize";
        }

        string prompt = PromptTemplates.IssuePrompt
            .Replace("{title}", stripHtml(issue.Title) ?? "")
            .Replace("{description}", stripHtml(issue.Description) ?? "");
            
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _config.LlmTemperature,
            MaxTokens = _config.LlmMaxTokens,
            Model = _config.LlmModel!,
        };
        
        LlmResponse response = await _llmService.GenerateAsync(request);
        return response.Success ? response.Content?.Trim() : null;
    }

    private async Task<string?> generateCommentSummary(SqliteConnection db, int issueId)
    {
        // Get comments for this issue
        List<CommentRecord> comments = CommentRecord.SelectList(db, IssueId: issueId);
        
        if (comments.Count == 0)
        {
            return "No comments";
        }
        
        string commentsText = string.Join("\n\n", comments.Select(c => 
            $"Comment by {c.Author} on {c.CreatedAt:yyyy-MM-dd}:\n{stripHtml(c.Body)}"));
            
        string prompt = PromptTemplates.CommentPrompt.Replace("{comments}", commentsText);
        
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _config.LlmTemperature,
            MaxTokens = _config.LlmMaxTokens,
            Model = _config.LlmModel!,
        };
        
        LlmResponse response = await _llmService.GenerateAsync(request);
        return response.Success ? response.Content?.Trim() : null;
    }

    private async Task<string?> generateResolutionSummary(IssueRecord issue)
    {
        if (string.IsNullOrEmpty(issue.ResolutionDescription))
        {
            return "No resolution information available";
        }
        
        string prompt = PromptTemplates.ResolutionPrompt
            .Replace("{resolution}", issue.Resolution ?? "")
            .Replace("{resolutionDescription}", stripHtml(issue.ResolutionDescription) ?? "");
            
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _config.LlmTemperature,
            MaxTokens = _config.LlmMaxTokens,
            Model = _config.LlmModel!,
        };
        
        LlmResponse response = await _llmService.GenerateAsync(request);
        return response.Success ? response.Content?.Trim() : null;
    }
}