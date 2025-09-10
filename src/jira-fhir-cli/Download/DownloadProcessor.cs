using System.Net.Http;
using System.Text;
using System.Web;

namespace jira_fhir_cli.Download;

/// <summary>
/// Main processor class that orchestrates JIRA download operations
/// </summary>
public class DownloadProcessor
{
    private const int _maxRetries = 3;
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);
    
    private readonly CliConfig _config;

    /// <summary>
    /// Initializes a new instance of the DownloadProcessor class
    /// </summary>
    /// <param name="config">The CLI configuration containing download parameters</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    /// <exception cref="ArgumentException">Thrown when required download configuration is missing</exception>
    public DownloadProcessor(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Validate required download fields
        validateConfiguration();
    }

    /// <summary>
    /// Main processing method that orchestrates the download operation
    /// </summary>
    /// <returns>A task representing the asynchronous download operation</returns>
    public async Task ProcessAsync()
    {
        Console.WriteLine("Starting JIRA Download process...");
        Console.WriteLine($"Output directory (Jira XML directory): {_config.JiraXmlDir}");
        
        if (!string.IsNullOrEmpty(_config.JiraSpecification))
        {
            Console.WriteLine($"Specification filter: {_config.JiraSpecification}");
        }
        
        if (_config.DownloadLimit.HasValue)
        {
            Console.WriteLine($"Day limit (download limit): {_config.DownloadLimit.Value} days");
        }

        try
        {
            // Ensure output directory exists
            ensureJiraXmlDirExists();

            // Generate date ranges to download
            List<DateRange> dateRanges = generateDateRanges();
            Console.WriteLine($"Generated {dateRanges.Count} date ranges for download.");

            // Setup HTTP client
            using HttpClient httpClient = setupHttpClient();
            Console.WriteLine("HTTP client configured.");

            // Download each date range
            List<DownloadResult> downloadResults = new();
            int currentDay = 1;
            int totalDays = dateRanges.Count;

            foreach (DateRange dateRange in dateRanges)
            {
                Console.WriteLine($"\n📅 Processing Day {currentDay} of {totalDays}: {dateRange.DisplayRange}");
                
                try
                {
                    DownloadResult result = await downloadDayAsync(httpClient, dateRange, currentDay, totalDays);
                    downloadResults.Add(result);
                    
                    if (result.IsSuccess)
                    {
                        Console.WriteLine($"✅ Day {currentDay} completed: {Path.GetFileName(result.FilePath!)} ({formatFileSize(result.FileSizeBytes!.Value)})");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Day {currentDay} failed: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Unexpected error downloading day {currentDay} ({dateRange.DisplayRange}): {ex.Message}";
                    downloadResults.Add(DownloadResult.Failure(dateRange, errorMessage));
                    Console.WriteLine($"❌ Day {currentDay} error: {errorMessage}");
                }

                currentDay++;
            }

            // Generate final results and manifest
            DownloadResults results = DownloadResults.Create(downloadResults, _config.JiraSpecification);
            await generateManifestAsync(results);

            // Print summary
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("📊 DOWNLOAD SUMMARY");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"📅 Total days processed: {results.TotalDays}");
            Console.WriteLine($"✅ Successful downloads: {results.SuccessCount}");
            Console.WriteLine($"❌ Failed downloads: {results.FailureCount}");
            Console.WriteLine($"📈 Success rate: {results.SuccessRate:F1}%");
            Console.WriteLine($"💾 Total data downloaded: {formatFileSize(results.TotalFileSizeBytes)}");
            Console.WriteLine(new string('=', 50));
            
            if (results.SuccessCount > 0)
            {
                Console.WriteLine("🎉 JIRA Download process completed successfully!");
            }
            else
            {
                Console.WriteLine("⚠️  JIRA Download process completed with no successful downloads.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error during download process: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates that the required configuration for downloads is present
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when required configuration is missing</exception>
    private void validateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.JiraCookie))
        {
            throw new ArgumentException("JiraCookie is required for download operations", nameof(_config.JiraCookie));
        }

        if (string.IsNullOrWhiteSpace(_config.JiraXmlDir))
        {
            throw new ArgumentException("JiraXmlDir is required for download operations", nameof(_config.JiraXmlDir));
        }
    }

    /// <summary>
    /// Generates a list of date ranges to download, starting from today and working backwards daily
    /// </summary>
    /// <returns>List of DateRange objects representing days to download</returns>
    private List<DateRange> generateDateRanges()
    {
        List<DateRange> dateRanges = new();
        DateTime today = DateTime.UtcNow.Date;
        
        // The earliest date we should download (2015-07-13)
        DateTime earliestDate = new DateTime(2015, 7, 13);
        
        // Validate that we're not trying to generate ranges before the earliest allowed date
        if (today < earliestDate)
        {
            Console.WriteLine($"Current date {today:yyyy-MM-dd} is before earliest allowed date {earliestDate:yyyy-MM-dd}");
            return dateRanges;
        }
        
        DateTime currentDate = today;
        int daysGenerated = 0;
        
        while (currentDate >= earliestDate)
        {
            // Check day limit if specified
            if (_config.DownloadLimit.HasValue && daysGenerated >= _config.DownloadLimit.Value)
            {
                break;
            }
            
            // Each day is its own range (same start and end date)
            dateRanges.Add(new DateRange(currentDate, currentDate));
            daysGenerated++;
            
            // Move to previous day
            currentDate = currentDate.AddDays(-1);
        }
        
        // Log summary of generated ranges
        if (dateRanges.Count > 0)
        {
            Console.WriteLine($"Generated {dateRanges.Count} daily ranges from {dateRanges[0].DisplayRange} back to {dateRanges[^1].DisplayRange}");
            
            if (_config.DownloadLimit.HasValue)
            {
                Console.WriteLine($"Applied day limit of {_config.DownloadLimit.Value} days");
            }
            
            // Show if we hit the historical boundary
            if (dateRanges.Count > 0 && dateRanges[^1].StartDate <= earliestDate)
            {
                Console.WriteLine($"Reached historical boundary of {earliestDate:yyyy-MM-dd}");
            }
        }
        else
        {
            Console.WriteLine("No daily ranges generated - current date may be before earliest allowed date");
        }
        
        return dateRanges;
    }

    /// <summary>
    /// Sets up and configures the HTTP client for JIRA API requests with all required headers
    /// </summary>
    /// <returns>Configured HttpClient instance</returns>
    private HttpClient setupHttpClient()
    {
        try
        {
            HttpClient client = new HttpClient();
            
            // Configure timeout
            client.Timeout = _timeout;
            
            // Validate cookie is present
            if (string.IsNullOrWhiteSpace(_config.JiraCookie))
            {
                throw new InvalidOperationException("JIRA cookie is required for authentication");
            }
            
            Console.WriteLine("Configuring HTTP client with required headers...");
            Console.WriteLine($"Request timeout: {_timeout.TotalSeconds} seconds");
            Console.WriteLine($"Cookie length: {_config.JiraCookie.Length} characters");
            
            // Add all required headers exactly as specified
            addRequiredHeaders(client);
            
            Console.WriteLine("HTTP client configured successfully with all required headers.");
            return client;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring HTTP client: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Adds all required headers to the HTTP client for successful JIRA authentication
    /// </summary>
    /// <param name="client">The HttpClient to configure</param>
    private void addRequiredHeaders(HttpClient client)
    {
        try
        {
            // Accept header - critical for proper content negotiation
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            
            // Accept-Language header
            client.DefaultRequestHeaders.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
            
            client.DefaultRequestHeaders.TryAddWithoutValidation("cache-control", "max-age=0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("dnt", "1");
            
            // Priority header
            client.DefaultRequestHeaders.TryAddWithoutValidation("priority", "u=0, i");
            
            // Sec-CH-UA headers (Chrome browser identification)
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "sec-ch-ua", 
                "\"Not;A=Brand\";v=\"99\", \"Microsoft Edge\";v=\"139\", \"Chromium\";v=\"139\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"macOS\"");
            
            // Sec-Fetch headers (security-related headers)
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-dest", "document");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-site", "same-origin");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-user", "?1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-gpc", "1");
            
            // Upgrade insecure requests
            client.DefaultRequestHeaders.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            
            // User-Agent header - critical for browser identification
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "user-agent", 
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0");
            
            // Authentication cookie - most critical header
            client.DefaultRequestHeaders.TryAddWithoutValidation("cookie", _config.JiraCookie);
            
            Console.WriteLine("All required headers added successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding headers to HTTP client: {ex.Message}");
            throw new InvalidOperationException("Failed to configure required headers for JIRA authentication", ex);
        }
    }
    
    /// <summary>
    /// Generates a dynamic referer URL based on the JQL query to mimic browser behavior
    /// </summary>
    /// <param name="jqlQuery">The JQL query to incorporate into the referer</param>
    /// <returns>Complete referer URL that looks like a JIRA search page</returns>
    private string generateRefererUrl(string jqlQuery)
    {
        try
        {
            // URL encode the JQL query for use in the referer
            string encodedJql = HttpUtility.UrlEncode(jqlQuery);
            
            // Create referer URL that looks like a user navigated from a search page
            string refererUrl = $"https://jira.hl7.org/issues/?jql={encodedJql}";
            
            Console.WriteLine($"Generated referer URL (JQL length: {jqlQuery.Length} chars)");
            
            return refererUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to generate referer URL: {ex.Message}");
            // Fallback to generic JIRA base URL
            return "https://jira.hl7.org/issues/";
        }
    }

    /// <summary>
    /// Downloads JIRA data for a specific date range using the configured HTTP client
    /// </summary>
    /// <param name="httpClient">The configured HttpClient to use for the request</param>
    /// <param name="dateRange">The date range to download</param>
    /// <param name="currentDay">Current day number being processed (for progress display)</param>
    /// <param name="totalDays">Total number of days to process (for progress display)</param>
    /// <returns>DownloadResult indicating success or failure</returns>
    private async Task<DownloadResult> downloadDayAsync(HttpClient httpClient, DateRange dateRange, int currentDay, int totalDays)
    {
        int attempt = 0;
        Exception? lastException = null;
        
        while (attempt < _maxRetries)
        {
            attempt++;
            
            try
            {
                Console.WriteLine($"  🔄 Attempt {attempt}/{_maxRetries} for Day {currentDay}");
                
                // Generate JQL query and download URL
                string jqlQuery = generateJqlQuery(dateRange);
                string downloadUrl = generateDownloadUrl(jqlQuery);
                string refererUrl = generateRefererUrl(jqlQuery);
                
                // Create request message to set referer header
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                request.Headers.TryAddWithoutValidation("referer", refererUrl);
                
                Console.WriteLine($"  📡 Making HTTP request to JIRA for Day {currentDay}...");
                // Console.WriteLine($"     - `{downloadUrl}`");
                
                // Make the HTTP request
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                
                Console.WriteLine($"  HTTP Status: {(int)response.StatusCode} {response.StatusCode}");
                
                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Generate file name using DayOf_YYYY-MM-DD.xml pattern (start date)
                    string fileName = generateFileName(dateRange);
                    string filePath = Path.Combine(_config.JiraXmlDir, fileName);
                    
                    // Read and save response content
                    string responseContent = await response.Content.ReadAsStringAsync();
                    
                    // Validate downloaded content
                    if (validateDownloadContent(responseContent, dateRange))
                    {
                        // Save the content to file with proper error handling
                        try
                        {
                            await File.WriteAllTextAsync(filePath, responseContent);
                            long fileSize = new FileInfo(filePath).Length;
                            
                            Console.WriteLine($"  ✓ Successfully downloaded {formatFileSize(fileSize)} to {Path.GetFileName(filePath)}");
                            return DownloadResult.Success(dateRange, filePath, fileSize);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            string errorMessage = $"Permission denied writing to {filePath}: {ex.Message}";
                            Console.WriteLine($"  ✗ File write error: {errorMessage}");
                            lastException = new InvalidOperationException(errorMessage, ex);
                        }
                        catch (DirectoryNotFoundException ex)
                        {
                            string errorMessage = $"Directory not found for {filePath}: {ex.Message}";
                            Console.WriteLine($"  ✗ Directory error: {errorMessage}");
                            lastException = new InvalidOperationException(errorMessage, ex);
                        }
                        catch (IOException ex)
                        {
                            string errorMessage = $"I/O error writing to {filePath}: {ex.Message}";
                            Console.WriteLine($"  ✗ I/O error: {errorMessage}");
                            lastException = new InvalidOperationException(errorMessage, ex);
                        }
                    }
                    else
                    {
                        // Content validation failed
                        string errorMessage = $"Downloaded content failed validation for {dateRange.DisplayRange}";
                        Console.WriteLine($"  ✗ Content validation failed");
                        lastException = new InvalidOperationException(errorMessage);
                    }
                }
                else
                {
                    string errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    
                    // Try to get response content for more details
                    try
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(errorContent) && errorContent.Length < 500)
                        {
                            errorMessage += $": {errorContent}";
                        }
                    }
                    catch
                    {
                        // Ignore errors reading error content
                    }
                    
                    Console.WriteLine($"  ✗ HTTP error: {errorMessage}");
                    lastException = new HttpRequestException(errorMessage);
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                string errorMessage = $"Request timeout after {_timeout.TotalSeconds} seconds";
                Console.WriteLine($"  ✗ {errorMessage}");
                lastException = new TimeoutException(errorMessage, ex);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"  ✗ HTTP request error: {ex.Message}");
                lastException = ex;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Unexpected error: {ex.Message}");
                lastException = ex;
            }
            
            // If we have more attempts, wait before retrying with exponential backoff
            if (attempt < _maxRetries)
            {
                int delaySeconds = (int)Math.Pow(2, attempt); // Exponential backoff: 2s, 4s, 8s
                Console.WriteLine($"  Retrying in {delaySeconds} seconds... (attempt {attempt + 1}/{_maxRetries})");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        
        // All attempts failed
        string finalError = lastException?.Message ?? "Unknown error occurred";
        Console.WriteLine($"  ❌ Day {currentDay}: All {_maxRetries} attempts failed. Final error: {finalError}");
        return DownloadResult.Failure(dateRange, finalError);
    }

    /// <summary>
    /// Generates a JQL query for a specific date range following JIRA specification requirements
    /// </summary>
    /// <param name="dateRange">The date range to query</param>
    /// <returns>JQL query string formatted for JIRA search</returns>
    /// <exception cref="ArgumentNullException">Thrown when dateRange parameter is null</exception>
    private string generateJqlQuery(DateRange dateRange)
    {
        if (dateRange == null)
        {
            throw new ArgumentNullException(nameof(dateRange), "Date range cannot be null");
        }

        // Format dates in JIRA-compatible format (YYYY-MM-DD)
        string startDate = dateRange.StartDate.ToString("yyyy-MM-dd");
        string endDate = dateRange.EndDate.ToString("yyyy-MM-dd");
        
        // Build base query according to specification:
        // project = "FHIR Specification Feedback" and updated <= '{endDate} 23:59' and updated >= '{startDate} 00:00' order by updated asc
        StringBuilder jql = new StringBuilder();
        
        // Add specification filter first if provided
        if (!string.IsNullOrEmpty(_config.JiraSpecification))
        {
            jql.Append($"Specification = \"{_config.JiraSpecification}\" and ");
        }
        
        // Add base query components
        jql.Append($"project = \"FHIR Specification Feedback\" and ");
        jql.Append($"updated <= '{endDate} 23:59' and ");
        jql.Append($"updated >= '{startDate} 00:00' ");
        jql.Append("order by updated asc");
        
        string jqlQuery = jql.ToString();
        Console.WriteLine($"Generated JQL: {jqlQuery}");
        
        return jqlQuery;
    }

    /// <summary>
    /// Generates the download URL from a JQL query for JIRA XML export
    /// </summary>
    /// <param name="jqlQuery">The JQL query string</param>
    /// <returns>Complete download URL for JIRA XML export</returns>
    /// <exception cref="ArgumentException">Thrown when jqlQuery is null or empty</exception>
    private string generateDownloadUrl(string jqlQuery)
    {
        if (string.IsNullOrWhiteSpace(jqlQuery))
        {
            throw new ArgumentException("JQL query cannot be null or empty", nameof(jqlQuery));
        }
        
        try
        {
            // URL encode the JQL query properly
            string encodedJql = HttpUtility.UrlEncode(jqlQuery);
            
            // Build complete JIRA export URL according to specification:
            // Base URL: https://jira.hl7.org/sr/jira.issueviews:searchrequest-xml/temp/SearchRequest.xml
            // Query parameters: jqlQuery={encodedJQL}&tempMax=1000
            string baseUrl = "https://jira.hl7.org/sr/jira.issueviews:searchrequest-xml/temp/SearchRequest.xml";
            string downloadUrl = $"{baseUrl}?jqlQuery={encodedJql}&tempMax=1000";
            
            Console.WriteLine($"Generated download URL for JQL query (length: {jqlQuery.Length} chars)");
            Console.WriteLine($"Encoded URL length: {downloadUrl.Length} chars");
            
            // Validate URL length doesn't exceed practical limits
            if (downloadUrl.Length > 2000)
            {
                Console.WriteLine($"WARNING: Generated URL is {downloadUrl.Length} characters long, which may exceed browser/server limits");
            }
            
            return downloadUrl;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate download URL from JQL query: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a comprehensive manifest file documenting all download results with detailed formatting
    /// </summary>
    /// <param name="results">The complete download results containing all successful and failed downloads</param>
    /// <returns>Task representing the asynchronous manifest generation operation</returns>
    /// <remarks>
    /// Creates a download_manifest.txt file in the output directory with:
    /// - Generation timestamp and configuration details
    /// - Download statistics (success/failure counts and rates)
    /// - Detailed list of successful downloads with file sizes
    /// - Detailed list of failed downloads with error messages
    /// - Professional formatting with visual separators
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when results parameter is null</exception>
    private async Task generateManifestAsync(DownloadResults results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results), "Download results cannot be null");
        }

        string manifestPath = Path.Combine(_config.JiraXmlDir, "download_manifest.txt");
        
        try
        {
            Console.WriteLine("📝 Generating download manifest...");
            
            StringBuilder manifest = new StringBuilder();
            
            // Header section with visual separators
            manifest.AppendLine("=============================================================");
            manifest.AppendLine("JIRA Download Manifest");
            manifest.AppendLine("=============================================================");
            manifest.AppendLine();
            
            // Generation and configuration details
            manifest.AppendLine($"Generation Time: {results.GeneratedAt:MMMM d, yyyy h:mm:ss tt} UTC");
            manifest.AppendLine($"Specification Filter: {results.SpecificationFilter ?? "None"}");
            manifest.AppendLine($"Output Directory: {_config.JiraXmlDir}");
            manifest.AppendLine();
            
            // Download statistics section
            manifest.AppendLine("DOWNLOAD STATISTICS:");
            manifest.AppendLine($"- Total days processed: {results.TotalDays}");
            manifest.AppendLine($"- Successful downloads: {results.SuccessCount}");
            manifest.AppendLine($"- Failed downloads: {results.FailureCount}");
            manifest.AppendLine($"- Success rate: {results.SuccessRate:F1}%");
            manifest.AppendLine($"- Total data downloaded: {formatFileSize(results.TotalFileSizeBytes)}");
            manifest.AppendLine();
            
            // Successful downloads section
            if (results.SuccessfulDownloads.Any())
            {
                manifest.AppendLine("SUCCESSFUL DOWNLOADS:");
                foreach (DownloadResult success in results.SuccessfulDownloads.OrderBy(r => r.DateRange.StartDate))
                {
                    string fileName = Path.GetFileName(success.FilePath!);
                    string fileSize = formatFileSize(success.FileSizeBytes!.Value);
                    manifest.AppendLine($"✓ {fileName} - Date Range: {success.DateRange.DisplayRange} - Size: {fileSize}");
                }
                manifest.AppendLine();
            }
            else
            {
                manifest.AppendLine("SUCCESSFUL DOWNLOADS:");
                manifest.AppendLine("None");
                manifest.AppendLine();
            }
            
            // Failed downloads section
            if (results.FailedDownloads.Any())
            {
                manifest.AppendLine("FAILED DOWNLOADS:");
                foreach (DownloadResult failure in results.FailedDownloads.OrderBy(r => r.DateRange.StartDate))
                {
                    manifest.AppendLine($"✗ Date Range: {failure.DateRange.DisplayRange} - Error: {failure.ErrorMessage}");
                }
                manifest.AppendLine();
            }
            else
            {
                manifest.AppendLine("FAILED DOWNLOADS:");
                manifest.AppendLine("None");
                manifest.AppendLine();
            }
            
            // Footer section
            manifest.AppendLine("=============================================================");
            manifest.AppendLine("Generated by JIRA FHIR CLI Download Tool");
            manifest.AppendLine("=============================================================");
            
            // Write manifest file with UTF-8 encoding
            await File.WriteAllTextAsync(manifestPath, manifest.ToString(), Encoding.UTF8);
            
            Console.WriteLine($"✅ Download manifest generated successfully: {Path.GetFileName(manifestPath)}");
            Console.WriteLine($"   Location: {manifestPath}");
        }
        catch (UnauthorizedAccessException ex)
        {
            string errorMessage = $"Permission denied writing manifest to {manifestPath}: {ex.Message}";
            Console.WriteLine($"❌ Manifest generation failed: {errorMessage}");
            // Don't throw - manifest generation failure shouldn't stop the process
        }
        catch (DirectoryNotFoundException ex)
        {
            string errorMessage = $"Directory not found for manifest {manifestPath}: {ex.Message}";
            Console.WriteLine($"❌ Manifest generation failed: {errorMessage}");
        }
        catch (IOException ex)
        {
            string errorMessage = $"I/O error writing manifest to {manifestPath}: {ex.Message}";
            Console.WriteLine($"❌ Manifest generation failed: {errorMessage}");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Unexpected error generating manifest: {ex.Message}";
            Console.WriteLine($"❌ Manifest generation failed: {errorMessage}");
        }
    }

    /// <summary>
    /// Ensures the output directory exists and handles creation gracefully
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when directory cannot be created</exception>
    private void ensureJiraXmlDirExists()
    {
        try
        {
            if (!Directory.Exists(_config.JiraXmlDir))
            {
                Console.WriteLine($"Creating output directory: {_config.JiraXmlDir}");
                Directory.CreateDirectory(_config.JiraXmlDir);
                Console.WriteLine("Output directory created successfully.");
            }
            else
            {
                Console.WriteLine("Output directory already exists and is accessible.");
            }
            
            // Test write access
            string testFile = Path.Combine(_config.JiraXmlDir, ".write_test");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Console.WriteLine("Output directory write permissions verified.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Output directory exists but is not writable: {ex.Message}", ex);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Insufficient permissions to create or access output directory '{_config.JiraXmlDir}': {ex.Message}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new InvalidOperationException($"Parent directory does not exist for output directory '{_config.JiraXmlDir}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"I/O error creating output directory '{_config.JiraXmlDir}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error ensuring output directory '{_config.JiraXmlDir}' exists: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates the file name using the DayOf_YYYY-MM-DD.xml pattern
    /// </summary>
    /// <param name="dateRange">The date range to generate filename for</param>
    /// <returns>File name in the format DayOf_YYYY-MM-DD.xml where the date is the start date</returns>
    /// <exception cref="ArgumentNullException">Thrown when dateRange parameter is null</exception>
    private static string generateFileName(DateRange dateRange)
    {
        if (dateRange == null)
        {
            throw new ArgumentNullException(nameof(dateRange), "Date range cannot be null");
        }

        // Use the start date for the file name in DayOf_YYYY-MM-DD.xml format
        string fileName = $"DayOf_{dateRange.StartDate:yyyy-MM-dd}.xml";
        
        return fileName;
    }

    /// <summary>
    /// Validates that the downloaded content appears to be valid XML and meets size expectations
    /// </summary>
    /// <param name="content">The downloaded content to validate</param>
    /// <param name="dateRange">The date range for context in error messages</param>
    /// <returns>True if content appears valid, false otherwise</returns>
    private static bool validateDownloadContent(string content, DateRange dateRange)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            Console.WriteLine($"  ⚠ Warning: Downloaded content is empty for {dateRange.DisplayRange}");
            return false;
        }

        // Check minimum reasonable size (at least 100 bytes for a minimal XML response)
        if (content.Length < 100)
        {
            Console.WriteLine($"  ⚠ Warning: Downloaded content is very small ({content.Length} bytes) for {dateRange.DisplayRange}");
        }

        // Validate XML format
        string trimmedContent = content.TrimStart();
        if (!trimmedContent.StartsWith("<"))
        {
            Console.WriteLine($"  ✗ Error: Content doesn't appear to be XML for {dateRange.DisplayRange}");
            Console.WriteLine($"    First 100 chars: {content.Substring(0, Math.Min(100, content.Length))}");
            return false;
        }

        // Check for common error indicators in HTML responses
        if (trimmedContent.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            trimmedContent.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            trimmedContent.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  ⚠ Warning: Content may be an error page rather than XML data for {dateRange.DisplayRange}");
        }

        return true;
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string
    /// </summary>
    /// <param name="bytes">The file size in bytes</param>
    /// <returns>Formatted file size string</returns>
    private static string formatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        double number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }
}