namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// One source cell that could not be processed as the recipe specified: a timestamp
/// transform whose value failed to parse, or a source cell that was unreadable. The cell
/// itself is still written to the output; the issue is only a data-quality signal.
/// </summary>
/// <param name="RowNumber">1-based data row number in the input file (header row excluded).</param>
/// <param name="ColumnName">The source column name in the input file.</param>
/// <param name="RawValue">The raw cell text captured at read time; empty when the source value was unreadable.</param>
/// <param name="Reason">Why the cell could not be processed.</param>
internal sealed record CellIssue(int RowNumber, string ColumnName, string RawValue, string Reason)
{
    /// <summary>
    /// Maximum number of detailed entries collected and reported per run. Beyond this only
    /// a "more than N" indicator is reported, so a wrong-column recipe cannot flood the
    /// output with one warning per row. Shared by the collection and reporting points.
    /// </summary>
    public const int MaxReportedIssues = 100;
}
