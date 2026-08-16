using System.Globalization;
using System.Text.Json;
using Refedle.Engine.IO.Json;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Traverses a KeyPath through a single record's bytes with an explicit-stack DFS, collecting leaf
/// rows via <see cref="KeyPathLeafCollector"/>. Descent depth is bounded by the heap, not the call
/// stack, so a keyPath whose length is driven by untrusted input cannot overflow the stack. Leaf
/// collection and value lookup live in <see cref="KeyPathLeafCollector"/> to keep both classes under
/// the per-class line limit and the dependency one-way (this class calls the collector, never back).
/// </summary>
internal static class KeyPathTraverser
{
    /// <summary>
    /// Traverses <paramref name="keyPath"/> starting from <paramref name="recordBytes"/> and
    /// collects the row(s) reached at the leaf, if any. Records where the path is absent or the
    /// token type mismatches a segment are silently skipped (no rows added).
    /// </summary>
    public static void ExtractRows(
        JsonRawBytes recordBytes,
        IReadOnlyList<KeyPathSegment> keyPath,
        string posHash,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        Stack<TraversalFrame> stack = [];
        stack.Push(TraversalFrame.Descend(recordBytes, 0, posHash));
        while (stack.TryPop(out var frame))
        {
            var (next, deferred) = ProcessFrame(
                frame, keyPath, colName, colNameUtf8,
                rows, keyOrder, keySet, columnTypes, keyObservedCount);
            if (deferred is { } d)
            {
                stack.Push(d);
            }

            if (next is { } n)
            {
                stack.Push(n);
            }
        }
    }

    /// <summary>
    /// Returns the last non-index segment of <paramref name="keyPath"/> — the column name used
    /// for a scalar primitive leaf. Falls back to <c>"value"</c> when every segment is an index
    /// segment (e.g. an empty or all-<c>[n]</c> path).
    /// </summary>
    public static string LastKeySegment(IReadOnlyList<KeyPathSegment> keyPath)
    {
        for (var i = keyPath.Count - 1; i >= 0; i--)
        {
            if (keyPath[i].Kind == KeyPathSegmentKind.Key)
            {
                return keyPath[i].Value;
            }
        }

        return "value";
    }

    private enum FrameKind { Descend, ContinueArray }

    /// <summary>
    /// Pending descent work. <see cref="FrameKind.Descend"/> applies the segment at
    /// <see cref="SegmentIndex"/> to <see cref="Bytes"/>. <see cref="FrameKind.ContinueArray"/>
    /// resumes an index segment's array scan from <see cref="ReaderState"/>; it is returned as the
    /// deferred frame after each element so only O(depth) frames stay live, never one per sibling.
    /// </summary>
    private readonly record struct TraversalFrame(
        FrameKind Kind,
        JsonRawBytes Bytes,
        int SegmentIndex,
        int ElementIndex,
        string PosHash,
        JsonReaderState ReaderState)
    {
        public static TraversalFrame Descend(JsonRawBytes bytes, int segmentIndex, string posHash) =>
            new(FrameKind.Descend, bytes, segmentIndex, 0, posHash, default);
    }

    // Returns the frame to descend next (its subtree processed first) and the frame to resume
    // afterward (an array scan continuation). The caller pushes deferred, then next, so the LIFO
    // stack finishes next's subtree before resuming deferred — preserving forward DFS order.
    private static (TraversalFrame? next, TraversalFrame? deferred) ProcessFrame(
        TraversalFrame frame,
        IReadOnlyList<KeyPathSegment> keyPath,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        if (frame.Kind == FrameKind.ContinueArray)
        {
            var reader = new Utf8JsonReader(frame.Bytes.Span, isFinalBlock: true, frame.ReaderState);
            return ScanOneArrayElement(ref reader, frame.Bytes, frame.SegmentIndex, frame.ElementIndex, frame.PosHash);
        }

        if (frame.SegmentIndex == keyPath.Count)
        {
            KeyPathLeafCollector.CollectLeafRows(
                frame.Bytes, frame.PosHash, colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return (null, null);
        }

        var segment = keyPath[frame.SegmentIndex];
        if (segment.Kind == KeyPathSegmentKind.Index)
        {
            return ExpandIndexSegment(frame, keyPath, rows, keyOrder, keySet, columnTypes, keyObservedCount);
        }

        var valueBytes = KeyPathLeafCollector.FindValueByKey(frame.Bytes, segment.Value);
        if (valueBytes is null)
        {
            return (null, null); // Key absent, or current value is not an object — skip record silently.
        }

        return (TraversalFrame.Descend(valueBytes.Value, frame.SegmentIndex + 1, frame.PosHash), null);
    }

    private static (TraversalFrame? next, TraversalFrame? deferred) ExpandIndexSegment(
        TraversalFrame frame,
        IReadOnlyList<KeyPathSegment> keyPath,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        var reader = new Utf8JsonReader(frame.Bytes.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return (null, null); // Wrong type at this path position — skip record silently.
        }

        if (frame.SegmentIndex == keyPath.Count - 1)
        {
            // A trailing index segment expands the same array that would be reached by selecting
            // it directly as the leaf (e.g. "tags" and "tags[0]" must produce identical output).
            KeyPathLeafCollector.CollectArrayLeafRows(frame.Bytes, frame.PosHash, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return (null, null);
        }

        return ScanOneArrayElement(ref reader, frame.Bytes, frame.SegmentIndex, 0, frame.PosHash);
    }

    // Reads the next depth-1 element and returns it as the next frame to descend, plus the
    // ContinueArray continuation for the remaining siblings as the deferred frame.
    private static (TraversalFrame? next, TraversalFrame? deferred) ScanOneArrayElement(
        ref Utf8JsonReader reader,
        JsonRawBytes arrayBytes,
        int segmentIndex,
        int elementIndex,
        string posHash)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return (null, null);
            }

            if (reader.CurrentDepth != 1)
            {
                continue;
            }

            var elementBytes = JsonByteExtractor.ExtractValueBytes(ref reader, arrayBytes);
            var remainder = arrayBytes.Slice((int)reader.BytesConsumed);
            var next = TraversalFrame.Descend(
                elementBytes,
                segmentIndex + 1,
                string.Create(CultureInfo.InvariantCulture, $"{posHash}:{elementIndex}")
            );
            var deferred = new TraversalFrame(
                FrameKind.ContinueArray, remainder, segmentIndex, elementIndex + 1, posHash, reader.CurrentState);
            return (next, deferred);
        }

        return (null, null);
    }
}
