namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Contract for all data transformation actions in the Action Stack.
/// </summary>
public interface IMorphAction
{
    /// <summary>
    /// Gets a human-readable description of the action for debugging and UI display.
    /// </summary>
    string Description { get; }
}
