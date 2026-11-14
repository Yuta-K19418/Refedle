using System.Text;
using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Low-level CSV row reader that reads raw CSV data from a file stream.
/// Returns rows as read-only lists of ReadOnlyMemory for memory efficiency.
/// </summary>
public sealed class DataRowReader : IDisposable
{
    private readonly string _filePath;
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
        _filePath = filePath;
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
    /// <returns>A list of CSV rows.</returns>
    public IReadOnlyList<CsvDataRow> ReadRows(long byteOffset, int rowsToSkip, int rowsToRead)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (byteOffset < 0 || rowsToRead <= 0)
        {
            return [];
        }

        var rows = new List<CsvDataRow>(rowsToRead);

        try
        {
            _fileStream.Seek(byteOffset, SeekOrigin.Begin);

            using var streamReader = new StreamReader(_fileStream, Encoding.UTF8, leaveOpen: true);
            using var reader = CsvSep.From(streamReader, hasHeader: false);

            SkipRows(reader, rowsToSkip);
            ReadRowsInto(reader, rowsToRead, rows);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Return empty list on I/O errors
            return [];
        }

        return rows;
    }

    /// <summary>
    /// Lazily enumerates the data rows from the start of the file in a single forward pass,
    /// skipping the header. Each enumeration opens its own read-only stream, so it can be
    /// interleaved with <see cref="ReadRows(long, int, int)"/> and other enumerations on the
    /// same reader. A row whose column count differs from the header's makes the enumeration
    /// throw. Unlike <see cref="ReadRows(long, int, int)"/>, I/O errors are not caught and
    /// propagate to the caller.
    /// </summary>
    /// <param name="rowsToSkip">Number of data rows to skip after the header.</param>
    /// <returns>A deferred sequence of CSV rows; the reader must stay undisposed while it is enumerated.</returns>
    /// <exception cref="ObjectDisposedException">
    /// The reader has been disposed, or is disposed during enumeration (thrown on the next advance).
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown during enumeration when a row's column count differs from the header's.
    /// </exception>
    public IEnumerable<CsvDataRow> EnumerateRows(int rowsToSkip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return EnumerateRowsCore(rowsToSkip);
    }

    private IEnumerable<CsvDataRow> EnumerateRowsCore(int rowsToSkip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Open a dedicated stream per enumeration so it never moves the shared stream's position
        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var streamReader = new StreamReader(fileStream, Encoding.UTF8);
        using var reader = CsvSep.From(streamReader, hasHeader: true);

        // Checking disposal before every advance keeps it observable even after the final row,
        // where the underlying reader would otherwise just report the end of input.
        bool advance()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return reader.MoveNext();
        }

        var skipped = 0;
        while (skipped < rowsToSkip && advance())
        {
            skipped++;
        }

        if (skipped < rowsToSkip)
        {
            yield break;
        }

        while (advance())
        {
            yield return ParseRow(reader.Current);
        }
    }

    private static void SkipRows(SepReader reader, int rowsToSkip)
    {
        var skipped = 0;
        while (skipped < rowsToSkip && reader.MoveNext())
        {
            skipped++;
        }
    }

    private void ReadRowsInto(SepReader reader, int rowsToRead, List<CsvDataRow> rows)
    {
        var readCount = 0;
        while (readCount < rowsToRead && reader.MoveNext())
        {
            var record = reader.Current;
            rows.Add(ParseRow(in record));
            readCount++;
        }
    }

    // Single-pass total length calculation, then a second pass to copy into one shared buffer -
    // avoids one allocation per column.
    private ReadOnlyMemory<char>[] ParseRow(in SepReader.Row record)
    {
        var totalLength = 0;
        for (var i = 0; i < _columnCount; i++)
        {
            if (i < record.ColCount)
            {
                totalLength += record[i].Span.Length;
            }
        }

        var buffer = new char[totalLength];
        var columns = new ReadOnlyMemory<char>[_columnCount];
        var bufferPos = 0;

        for (var i = 0; i < _columnCount; i++)
        {
            if (i >= record.ColCount)
            {
                columns[i] = ReadOnlyMemory<char>.Empty;
                continue;
            }

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
