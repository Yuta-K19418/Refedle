using System.Globalization;

namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Renders the cell issues collected during a successful batch run as end-of-run warnings.
/// Reported once after the run, not per row, so the worst case (a wrong-column recipe where
/// every row fails) stays bounded by <see cref="CellIssue.MaxReportedIssues"/> entries.
/// </summary>
internal static class CellIssueReporter
{
    /// <summary>
    /// Writes one warning per collected cell issue, introduced by a header line and closed
    /// by a "more than N" indicator when the detail list was capped. Writes nothing when
    /// there are no issues.
    /// </summary>
    /// <param name="cellIssues">The collected cell issues.</param>
    /// <param name="hasMoreCellIssues">Whether more cell issues existed than were collected.</param>
    /// <param name="logger">The app logger to write the warnings to.</param>
    public static async ValueTask ReportAsync(
        IReadOnlyList<CellIssue> cellIssues,
        bool hasMoreCellIssues,
        IAppLogger logger)
    {
        if (cellIssues.Count == 0)
        {
            return;
        }

        await logger.WriteWarningAsync("Some source cells could not be processed as specified:").ConfigureAwait(false);

        foreach (var issue in cellIssues)
        {
            await logger.WriteWarningAsync(FormatEntry(issue)).ConfigureAwait(false);
        }

        if (hasMoreCellIssues)
        {
            await logger.WriteWarningAsync(
                $"More than {CellIssue.MaxReportedIssues} source cells could not be processed; only the first {CellIssue.MaxReportedIssues} are listed.").ConfigureAwait(false);
        }
    }

    // Unreadable source cells have no text to show, so the raw value is omitted there.
    private static string FormatEntry(CellIssue issue)
    {
        var rowNumber = issue.RowNumber.ToString(CultureInfo.InvariantCulture);
        return issue.RawValue.Length == 0
            ? $"Row {rowNumber}, column \"{issue.ColumnName}\": {issue.Reason}"
            : $"Row {rowNumber}, column \"{issue.ColumnName}\": {issue.Reason} (\"{issue.RawValue}\")";
    }
}
