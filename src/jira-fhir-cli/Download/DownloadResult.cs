namespace jira_fhir_cli.Download;

/// <summary>
/// Represents the result of a single download attempt
/// </summary>
public record DownloadResult
{
    /// <summary>
    /// Gets the date range for this download attempt
    /// </summary>
    public DateRange DateRange { get; init; }

    /// <summary>
    /// Gets a value indicating whether the download was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the download failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the file path of the downloaded file if successful
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the size of the downloaded file in bytes if successful
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Initializes a new instance of the DownloadResult record
    /// </summary>
    /// <param name="dateRange">The date range for this download</param>
    /// <param name="isSuccess">Whether the download was successful</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <param name="filePath">Path to downloaded file if successful</param>
    /// <param name="fileSizeBytes">Size of downloaded file in bytes if successful</param>
    public DownloadResult(
        DateRange dateRange,
        bool isSuccess,
        string? errorMessage = null,
        string? filePath = null,
        long? fileSizeBytes = null)
    {
        DateRange = dateRange ?? throw new ArgumentNullException(nameof(dateRange));
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        FilePath = filePath;
        FileSizeBytes = fileSizeBytes;

        // Validation: successful downloads should have file path and size
        if (isSuccess && (string.IsNullOrEmpty(filePath) || !fileSizeBytes.HasValue))
        {
            throw new ArgumentException("Successful downloads must include file path and size");
        }

        // Validation: failed downloads should have error message
        if (!isSuccess && string.IsNullOrEmpty(errorMessage))
        {
            throw new ArgumentException("Failed downloads must include an error message");
        }
    }

    /// <summary>
    /// Creates a successful download result
    /// </summary>
    /// <param name="dateRange">The date range</param>
    /// <param name="filePath">Path to the downloaded file</param>
    /// <param name="fileSizeBytes">Size of the downloaded file in bytes</param>
    /// <returns>A successful DownloadResult</returns>
    public static DownloadResult Success(DateRange dateRange, string filePath, long fileSizeBytes)
    {
        return new DownloadResult(dateRange, true, null, filePath, fileSizeBytes);
    }

    /// <summary>
    /// Creates a failed download result
    /// </summary>
    /// <param name="dateRange">The date range</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>A failed DownloadResult</returns>
    public static DownloadResult Failure(DateRange dateRange, string errorMessage)
    {
        return new DownloadResult(dateRange, false, errorMessage);
    }
}