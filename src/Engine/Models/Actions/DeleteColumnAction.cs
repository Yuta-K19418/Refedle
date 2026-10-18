namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Represents an action to delete a column from the dataset.
/// </summary>
public sealed record DeleteColumnAction : IMorphAction
{
    /// <summary>
    /// Gets the name of the column to delete.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets a description of the action.
    /// </summary>
    public string Description => $"Delete column '{ColumnName}'";
}
