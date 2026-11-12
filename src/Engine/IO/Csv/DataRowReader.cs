using System.Globalization;
using System.Text;
using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Low-level CSV row reader that reads raw CSV data from a file stream.
/// Returns rows as read-only lists of ReadOnlyMemory for memory efficiency.
/// </summary>
public sealed class DataRowReader : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly int _columnCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="DataRowReader"/>.
    /// </summary>
    /// <param name="filePath">The path to the CSV file.</param>
    /// <param name="columnCount">The number of columns in the CSV file.</param>
    public DataRowReader(string filePath, int columnCount)
    {
        _columnCount = columnCount;
        _fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
    }

    /// <summary>
    /// Reads a specified number of rows from the CSV file starting at the given byte offset.
    /// </summary>
    /// <param name="byteOffset">The byte offset in the file to start reading from.</param>
    /// <param name="rowsToSkip">Number of rows to skip after seeking to the byte offset.</param>
    /// <param name="rowsToRead">Maximum number of rows to read.</param>
    /// <returns>
    /// The CSV rows, or a failure when a row does not have the header's column count. This covers
    /// both the rows in the window and the rows skipped between the checkpoint and the window.
    /// I/O errors yield an empty list.
    /// </returns>
    public Result<IReadOnlyList<CsvDataRow>> ReadRows(long byteOffset, int rowsToSkip, int rowsToRead)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (byteOffset < 0 || rowsToRead <= 0)
        {
            return Results.Success<IReadOnlyList<CsvDataRow>>([]);
        }

        var rows = new List<CsvDataRow>(rowsToRead);

        try
        {
            _fileStream.Seek(byteOffset, SeekOrigin.Begin);

            using var streamReader = new StreamReader(_fileStream, Encoding.UTF8, leaveOpen: true);
            using var reader = CsvSep.From(streamReader, hasHeader: false);

            if (!TrySkipRows(reader, rowsToSkip) || !TryReadRowsInto(reader, rowsToRead, rows))
            {
                return ColumnCountMismatch();
            }
        }
        catch (InvalidDataException)
        {
            // Sep compares each row with the first row it read, so a ragged window throws here
            return ColumnCountMismatch();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Return empty list on I/O errors
            return Results.Success<IReadOnlyList<CsvDataRow>>([]);
        }

        return Results.Success<IReadOnlyList<CsvDataRow>>(rows);
    }

    /// <summary>
    /// Lazily enumerates the data rows from the start of the file in a single forward pass,
    /// skipping the header. A row whose column count differs from the header's makes the
    /// enumeration throw. Unlike <see cref="ReadRows(long, int, int)"/>, I/O errors are not
    /// caught and propagate to the caller.
    /// </summary>
    /// <param name="rowsToSkip">Number of data rows to skip after the header.</param>
    /// <returns>A deferred sequence of CSV rows; the reader must stay undisposed while it is enumerated.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown during enumeration when a row's column count differs from the header's.
    /// </exception>
    public IEnumerable<CsvDataRow> EnumerateRows(int rowsToSkip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EnumerateRowsCore(rowsToSkip, skipMismatchedRows: false);
    }

    /// <summary>
    /// Same as <see cref="EnumerateRows(int)"/>, except that a row whose column count differs
    /// from the header's is skipped and enumeration continues with the next row. I/O errors
    /// are not caught and propagate to the caller.
    /// </summary>
    /// <param name="rowsToSkip">Number of data rows to skip after the header.</param>
    /// <returns>A deferred sequence of CSV rows; the reader must stay undisposed while it is enumerated.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    public IEnumerable<CsvDataRow> EnumerateRowsSkippingMismatches(int rowsToSkip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EnumerateRowsCore(rowsToSkip, skipMismatchedRows: true);
    }

    private IEnumerable<CsvDataRow> EnumerateRowsCore(int rowsToSkip, bool skipMismatchedRows)
    {
        _fileStream.Seek(0, SeekOrigin.Begin);

        using var streamReader = new StreamReader(_fileStream, Encoding.UTF8, leaveOpen: true);
        using var reader = CsvSep.From(streamReader, hasHeader: false);

        // The first row is the header; Sep checks every later row against its column count
        if (!reader.MoveNext())
        {
            yield break;
        }

        var skipped = 0;
        while (skipped < rowsToSkip && MoveNext(reader, skipMismatchedRows))
        {
            skipped++;
        }

        if (skipped < rowsToSkip)
        {
            yield break;
        }

        while (MoveNext(reader, skipMismatchedRows))
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            yield return ParseRow(reader.Current);
        }
    }

    // Verified: after an InvalidDataException, Sep has consumed the bad row and the next
    // MoveNext reads the following row. DataRowReaderTests pin this since Sep does not document it.
    private static bool MoveNext(SepReader reader, bool skipMismatchedRows)
    {
        while (true)
        {
            try
            {
                return reader.MoveNext();
            }
            catch (InvalidDataException) when (skipMismatchedRows)
            {
                // Only column-count mismatches are skipped; the next iteration reads the next row
            }
        }
    }

    private Result<IReadOnlyList<CsvDataRow>> ColumnCountMismatch() =>
        Results.Failure<IReadOnlyList<CsvDataRow>>(
            string.Create(
                CultureInfo.InvariantCulture,
                $"A row does not have the same number of columns as the header ({_columnCount})."));

    // Sep compares rows with the first row it read, which may itself be a skipped row,
    // so every skipped row is checked against the header's column count as well.
    private bool TrySkipRows(SepReader reader, int rowsToSkip)
    {
        var skipped = 0;
        while (skipped < rowsToSkip && reader.MoveNext())
        {
            if (reader.Current.ColCount != _columnCount)
            {
                return false;
            }

            skipped++;
        }

        return true;
    }

    private bool TryReadRowsInto(SepReader reader, int rowsToRead, List<CsvDataRow> rows)
    {
        var readCount = 0;
        while (readCount < rowsToRead && reader.MoveNext())
        {
            var record = reader.Current;
            if (record.ColCount != _columnCount)
            {
                return false;
            }

            rows.Add(ParseRow(in record));
            readCount++;
        }

        return true;
    }

    // Single-pass total length calculation, then a second pass to copy into one shared buffer -
    // avoids one allocation per column.
    private static ReadOnlyMemory<char>[] ParseRow(in SepReader.Row record)
    {
        var columnCount = record.ColCount;
        var totalLength = 0;
        for (var i = 0; i < columnCount; i++)
        {
            totalLength += record[i].Span.Length;
        }

        var buffer = new char[totalLength];
        var columns = new ReadOnlyMemory<char>[columnCount];
        var bufferPos = 0;

        for (var i = 0; i < columnCount; i++)
        {
            var colSpan = record[i].Span;
            colSpan.CopyTo(buffer.AsSpan(bufferPos, colSpan.Length));
            columns[i] = new ReadOnlyMemory<char>(buffer, bufferPos, colSpan.Length);
            bufferPos += colSpan.Length;
        }

        return columns;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fileStream.Dispose();
        _disposed = true;
    }
}
