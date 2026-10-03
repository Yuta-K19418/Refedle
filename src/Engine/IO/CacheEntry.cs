namespace Refedle.Engine.IO;

/// <summary>
/// Represents a cached row entry.
/// Stored as a struct to avoid a separate heap allocation per entry
/// when used as the value type in LinkedListNode.
/// </summary>
/// <typeparam name="TRow">The type of row data.</typeparam>
internal struct CacheEntry<TRow>
{
    internal long RowIndex;
    internal TRow Value;

    /// <summary>
    /// Resets the entry to its default/empty state.
    /// </summary>
    /// <param name="emptyValue">The empty/default value for the row type.</param>
    internal void Clear(TRow emptyValue)
    {
        RowIndex = -1;
        Value = emptyValue;
    }
}
