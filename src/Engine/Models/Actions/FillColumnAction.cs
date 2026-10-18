namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Represents an action to overwrite every value in a named column with a fixed string.
/// Use case: anonymization, masking, bulk initialization.
/// </summary>
public sealed record FillColumnAction : IMorphAction
{
    /// <summary>
    /// Gets the name of the column to fill.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets the fixed value to write into every cell of the column.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets a description of the action.
    /// </summary>
    public string Description => $"Fill column '{ColumnName}' with '{Value}'";
}
