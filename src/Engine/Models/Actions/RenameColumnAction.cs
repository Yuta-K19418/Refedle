namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Represents an action to rename a column in the dataset.
/// </summary>
public sealed record RenameColumnAction : IMorphAction
{
    /// <summary>
    /// Gets the current name of the column to rename.
    /// </summary>
    public required string OldName { get; init; }

    /// <summary>
    /// Gets the new name for the column.
    /// </summary>
    public required string NewName { get; init; }

    /// <summary>
    /// Gets a description of the action.
    /// </summary>
    public string Description => $"Rename column '{OldName}' to '{NewName}'";
}
