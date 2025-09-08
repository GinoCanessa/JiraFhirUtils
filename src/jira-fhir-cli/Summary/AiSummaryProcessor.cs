using Microsoft.Data.Sqlite;
using JiraFhirUtils.Common;
using jira_fhir_cli.LlmProvider;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;

namespace jira_fhir_cli.Summary;

public class AiSummaryProcessor
{
    private readonly CliConfig _config;
    private readonly ILlmProvider _llmProvider;
    private readonly SummaryConfiguration _summaryConfig;
    private readonly PromptTemplates _prompts;

    private static System.Text.RegularExpressions.Regex _htmlStripRegex = new("<.*?>", System.Text.RegularExpressions.RegexOptions.Compiled);

    public AiSummaryProcessor(CliConfig config)
    {
        _config = config;
        _summaryConfig = createSummaryConfiguration(config);
        _llmProvider = LlmProviderFactory.CreateProvider(_summaryConfig.LlmConfig);
        _prompts = _summaryConfig.Prompts;
    }

    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting AI Summarization process...");
        Console.WriteLine($"Using provider: {_llmProvider.ProviderName}");
        Console.WriteLine($"Using database: {_config.DbPath}");
        
        // Test connection first
        Console.WriteLine("Testing LLM connection...");
        if (!await _llmProvider.ValidateConnectionAsync())
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
        for (int i = 0; i < issuesToProcess.Count; i += _summaryConfig.BatchSize)
        {
            List<IssueRecord> batch = issuesToProcess.Skip(i).Take(_summaryConfig.BatchSize).ToList();
            await processBatch(connection, batch);
            
            Console.WriteLine($"Processed {Math.Min(i + _summaryConfig.BatchSize, issuesToProcess.Count)}/{issuesToProcess.Count} issues");
        }
        
        Console.WriteLine("AI Summarization complete!");
    }

    private SummaryConfiguration createSummaryConfiguration(CliConfig config)
    {
        // If a configuration file is provided, load from file
        if (!string.IsNullOrEmpty(config.LlmConfigPath))
        {
            return LlmProviderFactory.CreateSummaryConfigurationFromFile(config.LlmConfigPath);
        }

        // Otherwise, create from CLI options
        LlmConfiguration llmConfig;
        
        string providerName = config.LlmProvider?.ToLowerInvariant() ?? "lmstudio";
        switch (providerName)
        {
            case "lmstudio":
                llmConfig = LlmProviderFactory.CreateDefaultLMStudioConfig();
                break;
            case "openai":
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("OPENAI_API_KEY environment variable is required for OpenAI provider");
                }
                llmConfig = LlmProviderFactory.CreateDefaultOpenAIConfig(apiKey, config.LlmModel ?? "gpt-4o-mini");
                break;
            case "anthropic":
                string? claudeKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                if (string.IsNullOrEmpty(claudeKey))
                {
                    throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is required for Anthropic provider");
                }
                llmConfig = LlmProviderFactory.CreateDefaultAnthropicConfig(claudeKey, config.LlmModel ?? "claude-3-haiku-20240307");
                break;
            case "ollama":
                llmConfig = LlmProviderFactory.CreateDefaultOllamaConfig(config.LlmModel ?? "llama2", config.LlmApiEndpoint ?? "http://localhost:11434");
                break;
            case "azureopenai":
                string? azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                string? azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
                string? azureResource = Environment.GetEnvironmentVariable("AZURE_OPENAI_RESOURCE");
                
                if (string.IsNullOrEmpty(azureDeployment))
                {
                    throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable is required for Azure OpenAI provider");
                }
                
                if (string.IsNullOrEmpty(azureApiKey) && string.IsNullOrEmpty(config.LlmApiEndpoint) && string.IsNullOrEmpty(azureResource))
                {
                    throw new InvalidOperationException("Either AZURE_OPENAI_API_KEY or a valid endpoint/resource configuration is required for Azure OpenAI provider");
                }
                
                // Use deployment name as model if not overridden
                string model = config.LlmModel ?? azureDeployment;
                
                // Determine the endpoint
                string endpoint = config.LlmApiEndpoint ?? 
                    (!string.IsNullOrEmpty(azureResource) ? $"https://{azureResource}.openai.azure.com/" : "https://placeholder.openai.azure.com/");
                
                llmConfig = new LlmConfiguration
                {
                    ProviderType = LlmProviderType.AzureOpenAI,
                    ApiEndpoint = endpoint,
                    ApiKey = azureApiKey,
                    Model = model,
                    Temperature = 0.3,
                    MaxTokens = 500,
                    ProviderSpecificSettings = new Dictionary<string, object>
                    {
                        ["DeploymentName"] = azureDeployment,
                        ["ResourceName"] = azureResource ?? ""
                    }
                };
                break;
            default:
                throw new ArgumentException($"Unknown LLM provider: {providerName}");
        }

        // Override with CLI options if provided
        if (!string.IsNullOrEmpty(config.LlmApiEndpoint))
        {
            llmConfig = llmConfig with { ApiEndpoint = config.LlmApiEndpoint };
        }
        if (!string.IsNullOrEmpty(config.LlmModel))
        {
            llmConfig = llmConfig with { Model = config.LlmModel };
        }

        if (!string.IsNullOrEmpty(config.LlmApiVersion))
        {
            llmConfig = llmConfig with { ApiVersion = config.LlmApiVersion };
        }

        return new SummaryConfiguration
        {
            LlmConfig = llmConfig,
            BatchSize = config.BatchSize,
            OverwriteExistingSummaries = config.OverwriteSummaries,
            SummaryTypesToGenerate = SummaryTypes.All,
            Prompts = new PromptTemplates()
        };
    }

    private List<IssueRecord> getIssuesNeedingSummaries(SqliteConnection db)
    {
        List<IssueRecord> issues = _summaryConfig.OverwriteExistingSummaries
            ? IssueRecord.SelectList(db)
            : IssueRecord.SelectList(
                db,
                orderByProperties: [nameof(IssueRecord.Id)],
                orderByDirection: "DESC",
                orJoinConditions: true,
                AiIssueSummaryIsNull: true,
                AiCommentSummaryIsNull: true);

        if (_summaryConfig.OverwriteExistingSummaries)
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
        if (_summaryConfig.SummaryTypesToGenerate.HasFlag(SummaryTypes.Issue) && 
            (issue.AiIssueSummary == null || _summaryConfig.OverwriteExistingSummaries))
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
        if (_summaryConfig.SummaryTypesToGenerate.HasFlag(SummaryTypes.Comments) &&
            (issue.AiCommentSummary == null || _summaryConfig.OverwriteExistingSummaries))
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
        if (_summaryConfig.SummaryTypesToGenerate.HasFlag(SummaryTypes.Resolution) &&
            (issue.AiResolutionSummary == null || _summaryConfig.OverwriteExistingSummaries))
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

        string prompt = _prompts.IssuePrompt
            .Replace("{title}", stripHtml(issue.Title) ?? "")
            .Replace("{description}", stripHtml(issue.Description) ?? "");
            
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _summaryConfig.LlmConfig.Temperature,
            MaxTokens = _summaryConfig.LlmConfig.MaxTokens,
            Model = _summaryConfig.LlmConfig.Model
        };
        
        LlmResponse response = await _llmProvider.GenerateAsync(request);
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
            
        string prompt = _prompts.CommentPrompt.Replace("{comments}", commentsText);
        
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _summaryConfig.LlmConfig.Temperature,
            MaxTokens = _summaryConfig.LlmConfig.MaxTokens,
            Model = _summaryConfig.LlmConfig.Model
        };
        
        LlmResponse response = await _llmProvider.GenerateAsync(request);
        return response.Success ? response.Content?.Trim() : null;
    }

    private async Task<string?> generateResolutionSummary(IssueRecord issue)
    {
        if (string.IsNullOrEmpty(issue.ResolutionDescription))
        {
            return "No resolution information available";
        }
        
        string prompt = _prompts.ResolutionPrompt
            .Replace("{resolution}", issue.Resolution ?? "")
            .Replace("{resolutionDescription}", stripHtml(issue.ResolutionDescription) ?? "");
            
        LlmRequest request = new LlmRequest
        {
            Prompt = prompt,
            Temperature = _summaryConfig.LlmConfig.Temperature,
            MaxTokens = _summaryConfig.LlmConfig.MaxTokens,
            Model = _summaryConfig.LlmConfig.Model
        };
        
        LlmResponse response = await _llmProvider.GenerateAsync(request);
        return response.Success ? response.Content?.Trim() : null;
    }
}