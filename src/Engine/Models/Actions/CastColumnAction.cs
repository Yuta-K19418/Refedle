using Refedle.Engine.Types;

namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Represents an action to cast a column to a different data type.
/// </summary>
public sealed record CastColumnAction : IMorphAction
{
    /// <summary>
    /// Gets the name of the column to cast.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets the target type for the cast operation.
    /// </summary>
    public required ColumnType TargetType { get; init; }

    /// <summary>
    /// Gets a description of the action.
    /// </summary>
    public string Description => $"Cast column '{ColumnName}' to {TargetType}";
}
