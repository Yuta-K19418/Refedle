using Refedle.Engine.Models.Actions;

namespace Refedle.Engine.Models;

/// <summary>
/// Represents a reusable data transformation recipe containing an ordered sequence of actions.
/// Can be serialized to YAML for storage and reuse in headless CLI mode.
/// </summary>
public sealed record Recipe
{
    /// <summary>
    /// User-friendly name for the recipe.
    /// </summary>
    public required string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// Optional description of what this recipe does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The ordered list of transformation actions to apply.
    /// Actions are applied sequentially in the order they appear.
    /// </summary>
    public required IReadOnlyList<MorphAction> Actions { get; init; }

    /// <summary>
    /// Metadata: timestamp when the recipe was created or last modified.
    /// Uses DateTimeOffset to preserve timezone information.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>
    /// Returns true if the recipe contains no actions.
    /// </summary>
    public bool IsEmpty => Actions.Count == 0;
}
