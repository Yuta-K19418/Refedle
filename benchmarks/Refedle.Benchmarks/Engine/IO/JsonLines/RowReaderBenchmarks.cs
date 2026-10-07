using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.Engine.IO.JsonLines;

/// <summary>
/// Benchmarks JSON Lines line reading for short and long lines.
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
[MemoryDiagnoser]
public class RowReaderBenchmarks : IDisposable
{
    private const int ShortLineCount = 100_000;
    private const int ShortLineSkip = 50_000;
    private const int ShortLineRead = 300;
    private const int LongLineCount = 2_000;
    private const int LongLineSkip = 1_000;
    private const int LongLineRead = 100;
    private const int LongLinePayloadLength = 10_000;

    private readonly string _shortLinesFilePath;
    private readonly string _longLinesFilePath;
    private readonly RowReader _shortLinesReader;
    private readonly RowReader _longLinesReader;
    private bool _disposed;

    /// <summary>
    /// Creates benchmark data.
    /// </summary>
    public RowReaderBenchmarks()
    {
        _shortLinesFilePath = Path.GetTempFileName();
        _longLinesFilePath = Path.GetTempFileName();

        // Roughly 80 bytes per line
        var shortLines = new StringBuilder();
        for (var i = 0; i < ShortLineCount; i++)
        {
            var shortLine = $"{{\"id\":{i:D6},\"name\":\"User{i:D6}\",\"email\":\"user{i:D6}@example.com\",\"age\":{i % 100:D2}}}";
            shortLines.Append(shortLine).Append('\n');
        }

        File.WriteAllText(_shortLinesFilePath, shortLines.ToString());

        // Roughly 10 KB per line, which exceeds the 4 KB initial search window
        var payload = new string('x', LongLinePayloadLength);
        var longLines = new StringBuilder();
        for (var i = 0; i < LongLineCount; i++)
        {
            var longLine = $"{{\"id\":{i:D6},\"payload\":\"{payload}\"}}";
            longLines.Append(longLine).Append('\n');
        }

        File.WriteAllText(_longLinesFilePath, longLines.ToString());

        _shortLinesReader = new RowReader(_shortLinesFilePath);
        _longLinesReader = new RowReader(_longLinesFilePath);
    }

    /// <summary>
    /// Skips fifty thousand short lines without reading any.
    /// </summary>
    [Benchmark(Description = "ReadLines short lines (~80B), skip 50k only")]
    public int SkipShortLinesOnly()
    {
        var lines = _shortLinesReader.ReadLines(0, ShortLineSkip, 0);
        return lines.Count;
    }

    /// <summary>
    /// Reads three hundred short lines without skipping any.
    /// </summary>
    [Benchmark(Description = "ReadLines short lines (~80B), read 300 only")]
    public int ReadShortLinesOnly()
    {
        var lines = _shortLinesReader.ReadLines(0, 0, ShortLineRead);
        return lines.Count;
    }

    /// <summary>
    /// Skips one thousand long lines without reading any.
    /// </summary>
    [Benchmark(Description = "ReadLines long lines (~10KB), skip 1k only")]
    public int SkipLongLinesOnly()
    {
        var lines = _longLinesReader.ReadLines(0, LongLineSkip, 0);
        return lines.Count;
    }

    /// <summary>
    /// Reads one hundred long lines without skipping any.
    /// </summary>
    [Benchmark(Description = "ReadLines long lines (~10KB), read 100 only")]
    public int ReadLongLinesOnly()
    {
        var lines = _longLinesReader.ReadLines(0, 0, LongLineRead);
        return lines.Count;
    }

    /// <summary>
    /// Skips fifty thousand short lines and reads three hundred.
    /// </summary>
    [Benchmark(Description = "ReadLines short lines (~80B), skip 50k, read 300")]
    public int ReadLinesShortLinesAfterLargeSkip()
    {
        var lines = _shortLinesReader.ReadLines(0, ShortLineSkip, ShortLineRead);
        return lines.Count;
    }

    /// <summary>
    /// Skips one thousand long lines and reads one hundred.
    /// </summary>
    [Benchmark(Description = "ReadLines long lines (~10KB), skip 1k, read 100")]
    public int ReadLinesLongLinesAfterLargeSkip()
    {
        var lines = _longLinesReader.ReadLines(0, LongLineSkip, LongLineRead);
        return lines.Count;
    }

    /// <summary>
    /// Enumerates every short line.
    /// </summary>
    [Benchmark(Description = "EnumerateLines short lines (~80B), all 100k lines")]
    public int EnumerateAllShortLines()
    {
        var count = 0;
        foreach (var line in _shortLinesReader.EnumerateLines(0))
        {
            count += line.Length > 0 ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// Enumerates every long line.
    /// </summary>
    [Benchmark(Description = "EnumerateLines long lines (~10KB), all 2k lines")]
    public int EnumerateAllLongLines()
    {
        var count = 0;
        foreach (var line in _longLinesReader.EnumerateLines(0))
        {
            count += line.Length > 0 ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// Releases benchmark resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases benchmark resources.
    /// </summary>
    /// <param name="disposing">Indicates whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _shortLinesReader.Dispose();
        _longLinesReader.Dispose();
        File.Delete(_shortLinesFilePath);
        File.Delete(_longLinesFilePath);
        _disposed = true;
    }
}
