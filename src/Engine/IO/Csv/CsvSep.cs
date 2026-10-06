using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Creates Sep readers configured for comma-separated files, with quoted values unescaped.
/// </summary>
public static class CsvSep
{
    /// <summary>
    /// Opens a CSV file for reading.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <param name="hasHeader">Whether the first row is a header.</param>
    /// <returns>The reader over the file.</returns>
    public static SepReader FromFile(string filePath, bool hasHeader = true)
    {
        var options = CreateOptions(hasHeader);
        return options.FromFile(filePath);
    }

    /// <summary>
    /// Opens a CSV file for reading asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <param name="hasHeader">Whether the first row is a header.</param>
    /// <param name="cancellationToken">Token to cancel the open operation.</param>
    /// <returns>The reader over the file.</returns>
    public static ValueTask<SepReader> FromFileAsync(
        string filePath,
        bool hasHeader = true,
        CancellationToken cancellationToken = default)
    {
        var options = CreateOptions(hasHeader);
        return options.FromFileAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Reads CSV data from a stream reader.
    /// </summary>
    /// <param name="streamReader">The reader to read from.</param>
    /// <param name="hasHeader">Whether the first row is a header.</param>
    /// <returns>The reader over the stream.</returns>
    public static SepReader From(StreamReader streamReader, bool hasHeader = true)
    {
        var options = CreateOptions(hasHeader);
        return options.From(streamReader);
    }

    private static SepReaderOptions CreateOptions(bool hasHeader) =>
        new(Sep.New(',')) { HasHeader = hasHeader, Unescape = true };
}
