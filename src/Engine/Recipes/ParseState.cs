namespace Refedle.Engine.Recipes;

/// <summary>
/// Represents the state of the YAML parser during deserialization.
/// </summary>
internal enum ParseState
{
    /// <summary>
    /// Parsing top-level key: value pairs.
    /// </summary>
    Root,

    /// <summary>
    /// Encountered actions: or actions: []; ready for list items.
    /// </summary>
    Actions,

    /// <summary>
    /// Accumulating key: value pairs for the current action item.
    /// </summary>
    ActionItem,

    /// <summary>
    /// Encountered drillDownKeyPath:; accumulating key/index fields for the current segment item.
    /// </summary>
    DrillDownKeyPathItem,
}
