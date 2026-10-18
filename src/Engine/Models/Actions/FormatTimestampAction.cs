namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Action that reformats the string representation of date/time values in a Timestamp column.
/// </summary>
public sealed record FormatTimestampAction : IMorphAction
{
    /// <summary>
    /// Gets the name of the column to format.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// .NET date/time format string that all values will be reformatted to.
    /// </summary>
    public required string TargetFormat { get; init; }

    /// <summary>
    /// Gets a human-readable description of this action.
    /// </summary>
    public string Description =>
        $"Format timestamp column '{ColumnName}' → \"{TargetFormat}\"";
}
