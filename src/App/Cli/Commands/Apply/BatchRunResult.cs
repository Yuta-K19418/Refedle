namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// The outcome of one batch run: the exit code plus the source-cell issues collected while
/// processing, capped at <see cref="CellIssue.MaxReportedIssues"/> detailed entries.
/// </summary>
/// <param name="ExitCode">The exit code of the batch run.</param>
/// <param name="CellIssues">The collected cell issues, at most <see cref="CellIssue.MaxReportedIssues"/> entries.</param>
/// <param name="HasMoreCellIssues">Whether more cell issues existed than were kept in <paramref name="CellIssues"/>.</param>
internal sealed record BatchRunResult(
    ExitCode ExitCode,
    IReadOnlyList<CellIssue> CellIssues,
    bool HasMoreCellIssues);
