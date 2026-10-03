using System.Diagnostics;

namespace Refedle.Engine.IO;

/// <summary>
/// Provides specialized extension methods for <see cref="LinkedList{T}"/> of <see cref="CacheEntry{TRow}"/>
/// to encapsulate LRU mechanics and node reuse.
/// </summary>
internal static class LinkedListExtensions
{
    /// <summary>
    /// Moves an existing node to the front (Most Recently Used) position of the list.
    /// </summary>
    public static void MoveToFront<TRow>(
        this LinkedList<CacheEntry<TRow>> list,
        LinkedListNode<CacheEntry<TRow>> node
    )
    {
        list.Remove(node);
        list.AddFirst(node);
    }

    /// <summary>
    /// Reuses the tail (Least Recently Used) node for a new row, updating the cache dictionary
    /// and moving the node to the front.
    /// </summary>
    public static void ReuseTail<TRow>(
        this LinkedList<CacheEntry<TRow>> list,
        Dictionary<long, LinkedListNode<CacheEntry<TRow>>> cache,
        long rowIndex,
        TRow rowValue,
        TRow emptyValue
    )
    {
        var node = list.Last ?? throw new UnreachableException("LRU list must be non-empty.");
        list.Remove(node);

        // Remove old entry from cache dictionary
        cache.Remove(node.ValueRef.RowIndex);

        // Update node state
        node.ValueRef.Clear(emptyValue);
        node.ValueRef.RowIndex = rowIndex;
        node.ValueRef.Value = rowValue;

        // Add to front and update cache dictionary
        list.AddFirst(node);
        cache[rowIndex] = node;
    }

    /// <summary>
    /// Creates a new node for the given row, adds it to the front of the list,
    /// and registers it in the cache dictionary.
    /// </summary>
    public static void AddNew<TRow>(
        this LinkedList<CacheEntry<TRow>> list,
        Dictionary<long, LinkedListNode<CacheEntry<TRow>>> cache,
        long rowIndex,
        TRow rowValue
    )
    {
        var node = new LinkedListNode<CacheEntry<TRow>>(
            new CacheEntry<TRow> { RowIndex = rowIndex, Value = rowValue }
        );
        list.AddFirst(node);
        cache[rowIndex] = node;
    }
}
