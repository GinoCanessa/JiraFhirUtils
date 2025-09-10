namespace jira_fhir_cli.Download.Models;

/// <summary>
/// Aggregated results with success/failure tracking for multiple downloads
/// </summary>
public record DownloadResults
{
    /// <summary>
    /// Gets the list of all download results
    /// </summary>
    public List<DownloadResult> Results { get; init; }

    /// <summary>
    /// Gets the date and time when these results were generated
    /// </summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Gets the specification filter that was used for the downloads
    /// </summary>
    public string? SpecificationFilter { get; init; }

    /// <summary>
    /// Initializes a new instance of the DownloadResults record
    /// </summary>
    /// <param name="results">The list of download results</param>
    /// <param name="generatedAt">When the results were generated</param>
    /// <param name="specificationFilter">The specification filter used</param>
    public DownloadResults(
        List<DownloadResult> results,
        DateTime generatedAt,
        string? specificationFilter = null)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
        GeneratedAt = generatedAt;
        SpecificationFilter = specificationFilter;
    }

    /// <summary>
    /// Gets the total number of days attempted
    /// </summary>
    public int TotalDays => Results.Count;

    /// <summary>
    /// Gets the number of successful downloads
    /// </summary>
    public int SuccessCount => Results.Count(r => r.IsSuccess);

    /// <summary>
    /// Gets the number of failed downloads
    /// </summary>
    public int FailureCount => Results.Count(r => !r.IsSuccess);

    /// <summary>
    /// Gets the success rate as a percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalDays == 0 ? 0 : (double)SuccessCount / TotalDays * 100;

    /// <summary>
    /// Gets the total size of all successfully downloaded files in bytes
    /// </summary>
    public long TotalFileSizeBytes => Results
        .Where(r => r.IsSuccess && r.FileSizeBytes.HasValue)
        .Sum(r => r.FileSizeBytes!.Value);

    /// <summary>
    /// Gets all successful download results
    /// </summary>
    public IEnumerable<DownloadResult> SuccessfulDownloads => Results.Where(r => r.IsSuccess);

    /// <summary>
    /// Gets all failed download results
    /// </summary>
    public IEnumerable<DownloadResult> FailedDownloads => Results.Where(r => !r.IsSuccess);

    /// <summary>
    /// Creates a new DownloadResults instance with the current timestamp
    /// </summary>
    /// <param name="results">The list of download results</param>
    /// <param name="specificationFilter">The specification filter used</param>
    /// <returns>A new DownloadResults instance</returns>
    public static DownloadResults Create(List<DownloadResult> results, string? specificationFilter = null)
    {
        return new DownloadResults(results, DateTime.UtcNow, specificationFilter);
    }
}