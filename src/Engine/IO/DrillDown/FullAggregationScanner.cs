using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Full-file DrillDown scan: traverses a KeyPath for every record in the file and
/// collects the leaf values reached, building a union <see cref="TableSchema"/> across all
/// collected rows. Supports all leaf types: JSON Object, JSON Array of Object,
/// JSON Array of Primitive, and scalar Primitive.
/// </summary>
public static class FullAggregationScanner
{
    /// <summary>
    /// Scans <paramref name="filePath"/> for all records and traverses <paramref name="keyPath"/>
    /// to collect leaf values of every matching path. Supports all leaf types: JSON Object,
    /// JSON Array of Object, JSON Array of Primitive, and scalar Primitive.
    /// Returns <c>Failure</c> when format is JSON Object, no rows are collected, or
    /// all collected leaf objects have no keys.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)> Scan(
        string filePath,
        DataFormat format,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(keyPath);

        if (format == DataFormat.JsonObject)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<FocusedTableRow>)>(
                "JSON Object format does not support full aggregation.");
        }

        Result<MmapService> mmapResult = MmapService.Open(filePath);
        if (mmapResult.IsFailure)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<FocusedTableRow>)>(mmapResult.Error);
        }

        using MmapService mmap = mmapResult.Value;

        ScanData scanData = format switch
        {
            DataFormat.JsonLines => ScanLines(mmap, keyPath, cancellationToken),
            DataFormat.JsonArray => ScanElements(mmap, keyPath, cancellationToken),
            _ => throw new UnreachableException($"Full aggregation scan does not handle format '{format}'."),
        };

        if (scanData.Rows.Count == 0)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<FocusedTableRow>)>("No matching records found.");
        }

        if (scanData.KeyOrder.Count == 0)
        {
            return Results.Failure<(TableSchema, IReadOnlyList<FocusedTableRow>)>("All child objects have no keys");
        }

        TableSchema schema = SchemaScanner.BuildTableSchema(scanData.KeyOrder, scanData.ColumnTypes, scanData.KeyObservedCount, scanData.Rows.Count, format);
        return Results.Success<(TableSchema, IReadOnlyList<FocusedTableRow>)>((schema, scanData.Rows));
    }

    private static ScanData ScanLines(
        MmapService mmap,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken cancellationToken)
    {
        string colName = KeyPathTraverser.LastKeySegment(keyPath);
        byte[] colNameUtf8 = Encoding.UTF8.GetBytes(colName);

        List<FocusedTableRow> rows = [];
        List<string> keyOrder = [];
        HashSet<string> keySet = new(StringComparer.Ordinal);
        Dictionary<string, ColumnType> columnTypes = new(StringComparer.Ordinal);
        Dictionary<string, int> keyObservedCount = new(StringComparer.Ordinal);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(FileChunkReader.BufferSize);
        try
        {
            long fileOffset = mmap.GetOffsetAfterUtf8Bom();
            long recordPosition = 1L;
            int remainingLen = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (int dataEnd, fileOffset) = FileChunkReader.FillBuffer(mmap, buffer, remainingLen, fileOffset, "JSON line");
                bool isFinalBlock = fileOffset >= mmap.Length;
                int consumed = 0;

                while (true)
                {
                    int newlineIndex = buffer.AsSpan(consumed, dataEnd - consumed).IndexOf((byte)'\n');
                    if (newlineIndex == -1)
                    {
                        break;
                    }

                    ExtractAndProcessLine(
                        buffer.AsSpan(consumed, newlineIndex), recordPosition, keyPath,
                        colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
                    recordPosition++;
                    consumed += newlineIndex + 1;
                }

                if (isFinalBlock)
                {
                    ExtractAndProcessLine(
                        buffer.AsSpan(consumed, dataEnd - consumed), recordPosition, keyPath,
                        colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
                    return new ScanData(rows, keyOrder, columnTypes, keyObservedCount);
                }

                remainingLen = dataEnd - consumed;
                if (remainingLen > 0)
                {
                    buffer.AsSpan(consumed, remainingLen).CopyTo(buffer);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ExtractAndProcessLine(
        ReadOnlySpan<byte> lineSpan,
        long recordPosition,
        IReadOnlyList<KeyPathSegment> keyPath,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        ReadOnlySpan<byte> trimmed = FileChunkReader.TrimTrailingCr(lineSpan);
        if (trimmed.IsEmpty)
        {
            return;
        }

        KeyPathTraverser.ExtractRows(
            trimmed.ToArray(), keyPath, recordPosition.ToString(CultureInfo.InvariantCulture),
            colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
    }

    private static ScanData ScanElements(
        MmapService mmap,
        IReadOnlyList<KeyPathSegment> keyPath,
        CancellationToken cancellationToken)
    {
        string colName = KeyPathTraverser.LastKeySegment(keyPath);
        byte[] colNameUtf8 = Encoding.UTF8.GetBytes(colName);

        List<FocusedTableRow> rows = [];
        List<string> keyOrder = [];
        HashSet<string> keySet = new(StringComparer.Ordinal);
        Dictionary<string, ColumnType> columnTypes = new(StringComparer.Ordinal);
        Dictionary<string, int> keyObservedCount = new(StringComparer.Ordinal);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(FileChunkReader.BufferSize);
        try
        {
            JsonReaderState state = default(JsonReaderState);
            long fileReadOffset = mmap.GetOffsetAfterUtf8Bom();
            long bufferOriginFileOffset = fileReadOffset;
            int remainingLen = 0;
            long recordPosition = 0L;
            long currentElementStart = -1L;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (int dataEnd, fileReadOffset) = FileChunkReader.FillBuffer(mmap, buffer, remainingLen, fileReadOffset, "JSON element");
                bool isFinalBlock = fileReadOffset >= mmap.Length;

                Utf8JsonReader reader = new(buffer.AsSpan(0, dataEnd), isFinalBlock, state);
                bool rootDone = false;

                while (!rootDone && reader.Read())
                {
                    (rootDone, currentElementStart, recordPosition) = ProcessElementToken(
                        ref reader, mmap, bufferOriginFileOffset, currentElementStart, recordPosition,
                        keyPath, colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
                }

                if (rootDone)
                {
                    return new ScanData(rows, keyOrder, columnTypes, keyObservedCount);
                }

                state = reader.CurrentState;
                int consumed = (int)reader.BytesConsumed;
                bufferOriginFileOffset += consumed;
                remainingLen = dataEnd - consumed;
                if (remainingLen > 0)
                {
                    buffer.AsSpan(consumed, remainingLen).CopyTo(buffer);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Handles a single token read from the top-level array during <see cref="ScanElements"/>,
    /// tracking the start of the current element and dispatching a completed element (or a
    /// depth-1 primitive) to <see cref="KeyPathTraverser.ExtractRows"/>.
    /// </summary>
    /// <returns>
    /// Whether the root array has ended, plus the updated <c>currentElementStart</c> and
    /// <c>recordPosition</c> to carry into the next token.
    /// </returns>
    private static (bool isRootDone, long currentElementStart, long recordPosition) ProcessElementToken(
        ref Utf8JsonReader reader,
        MmapService mmap,
        long bufferOriginFileOffset,
        long currentElementStart,
        long recordPosition,
        IReadOnlyList<KeyPathSegment> keyPath,
        string colName,
        byte[] colNameUtf8,
        List<FocusedTableRow> rows,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount)
    {
        if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.EndArray)
        {
            return (true, currentElementStart, recordPosition);
        }

        if (reader.CurrentDepth != 1)
        {
            return (false, currentElementStart, recordPosition);
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            long updatedStart = currentElementStart < 0
                ? bufferOriginFileOffset + reader.TokenStartIndex
                : currentElementStart;
            return (false, updatedStart, recordPosition);
        }

        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            long elementEnd = bufferOriginFileOffset + reader.BytesConsumed;
            JsonRawBytes elementBytes = FileChunkReader.ReadFileRange(mmap, currentElementStart, elementEnd);
            KeyPathTraverser.ExtractRows(
                elementBytes, keyPath, recordPosition.ToString(CultureInfo.InvariantCulture),
                colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
            return (false, -1L, recordPosition + 1);
        }

        // Primitive element at depth 1 (number, string, bool, null).
        JsonRawBytes primitiveBytes = FileChunkReader.ReadFileRange(
            mmap, bufferOriginFileOffset + reader.TokenStartIndex, bufferOriginFileOffset + reader.BytesConsumed);
        KeyPathTraverser.ExtractRows(
            primitiveBytes, keyPath, recordPosition.ToString(CultureInfo.InvariantCulture),
            colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount);
        return (false, currentElementStart, recordPosition + 1);
    }

    private readonly record struct ScanData(
        List<FocusedTableRow> Rows,
        List<string> KeyOrder,
        Dictionary<string, ColumnType> ColumnTypes,
        Dictionary<string, int> KeyObservedCount);
}
