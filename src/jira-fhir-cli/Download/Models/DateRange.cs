namespace jira_fhir_cli.Download.Models;

/// <summary>
/// Represents a date range for downloading JIRA data
/// </summary>
public record DateRange
{
    /// <summary>
    /// Gets the start date of the date range
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Gets the end date of the date range
    /// </summary>
    public DateTime EndDate { get; init; }

    /// <summary>
    /// Initializes a new instance of the DateRange record
    /// </summary>
    /// <param name="startDate">The start date of the range</param>
    /// <param name="endDate">The end date of the range</param>
    /// <exception cref="ArgumentException">Thrown when end date is before start date</exception>
    public DateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date cannot be before start date", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Gets a formatted display string for the date range
    /// </summary>
    public string DisplayRange => $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}";

    /// <summary>
    /// Gets the number of days in the date range
    /// </summary>
    public int DurationDays => (EndDate - StartDate).Days + 1;
}