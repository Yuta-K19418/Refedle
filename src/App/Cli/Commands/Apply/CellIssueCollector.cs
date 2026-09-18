namespace Refedle.App.Cli.Commands.Apply;

/// <summary>
/// Accumulates the cell issues of one run, keeping at most <see cref="CellIssue.MaxReportedIssues"/>
/// detailed entries and only flagging that more existed (accumulation across loop iterations).
/// </summary>
internal sealed class CellIssueCollector
{
    private readonly List<CellIssue> _issues = [];

    public bool HasMore { get; private set; }

    public IReadOnlyList<CellIssue> Issues => _issues;

    /// <summary>
    /// Takes the raw value as a span so it is materialized to a string only after the cap
    /// check passes — once capped, a failing cell costs no allocation. The span is consumed
    /// synchronously here, never stored.
    /// </summary>
    public void Add(int rowNumber, string columnName, ReadOnlySpan<char> rawValue, string reason)
    {
        if (_issues.Count >= CellIssue.MaxReportedIssues)
        {
            HasMore = true;
            return;
        }

        _issues.Add(new CellIssue(rowNumber, columnName, rawValue.ToString(), reason));
    }
}
