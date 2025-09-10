namespace jira_fhir_cli.Download.Models;

/// <summary>
/// Configuration options for Jira download operations
/// </summary>
public record JiraDownloadOptions
{
    /// <summary>
    /// Gets the Jira authentication cookie
    /// </summary>
    public string JiraCookie { get; init; }

    /// <summary>
    /// Gets the output directory path where downloaded files will be saved
    /// </summary>
    public string OutputDirectory { get; init; }

    /// <summary>
    /// Gets the optional specification filter to apply to downloads
    /// </summary>
    public string? SpecificationFilter { get; init; }

    /// <summary>
    /// Gets the optional limit on the number of days to download
    /// </summary>
    public int? DayLimit { get; init; }

    /// <summary>
    /// Gets the number of retry attempts for failed downloads
    /// </summary>
    public int RetryAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the HTTP request timeout duration
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the JiraDownloadOptions record
    /// </summary>
    /// <param name="jiraCookie">The Jira authentication cookie</param>
    /// <param name="outputDirectory">The output directory path</param>
    /// <param name="specificationFilter">Optional specification filter</param>
    /// <param name="dayLimit">Optional limit on days to download</param>
    /// <param name="retryAttempts">Number of retry attempts (default: 3)</param>
    /// <param name="requestTimeout">HTTP request timeout (default: 30 seconds)</param>
    /// <exception cref="ArgumentException">Thrown when required parameters are invalid</exception>
    public JiraDownloadOptions(
        string jiraCookie,
        string outputDirectory,
        string? specificationFilter = null,
        int? dayLimit = null,
        int retryAttempts = 3,
        TimeSpan? requestTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(jiraCookie))
        {
            throw new ArgumentException("Jira cookie cannot be null or empty", nameof(jiraCookie));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty", nameof(outputDirectory));
        }

        if (retryAttempts < 0)
        {
            throw new ArgumentException("Retry attempts cannot be negative", nameof(retryAttempts));
        }

        if (dayLimit.HasValue && dayLimit.Value <= 0)
        {
            throw new ArgumentException("Day limit must be positive if specified", nameof(dayLimit));
        }

        JiraCookie = jiraCookie;
        OutputDirectory = outputDirectory;
        SpecificationFilter = specificationFilter;
        DayLimit = dayLimit;
        RetryAttempts = retryAttempts;
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(30);

        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Request timeout must be positive", nameof(requestTimeout));
        }
    }

    /// <summary>
    /// Creates a copy of the current options with updated values
    /// </summary>
    /// <param name="jiraCookie">Updated Jira cookie</param>
    /// <param name="outputDirectory">Updated output directory</param>
    /// <param name="specificationFilter">Updated specification filter</param>
    /// <param name="dayLimit">Updated day limit</param>
    /// <param name="retryAttempts">Updated retry attempts</param>
    /// <param name="requestTimeout">Updated request timeout</param>
    /// <returns>A new JiraDownloadOptions instance with updated values</returns>
    public JiraDownloadOptions With(
        string? jiraCookie = null,
        string? outputDirectory = null,
        string? specificationFilter = null,
        int? dayLimit = null,
        int? retryAttempts = null,
        TimeSpan? requestTimeout = null)
    {
        return new JiraDownloadOptions(
            jiraCookie ?? JiraCookie,
            outputDirectory ?? OutputDirectory,
            specificationFilter ?? SpecificationFilter,
            dayLimit ?? DayLimit,
            retryAttempts ?? RetryAttempts,
            requestTimeout ?? RequestTimeout);
    }
}