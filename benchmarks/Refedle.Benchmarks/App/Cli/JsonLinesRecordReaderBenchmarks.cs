using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.App.Cli;

/// <summary>
/// Verifies GetCellData stays allocation-free for Number, String, Object, and Array,
/// whose distinct parsing branches need independent coverage.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class JsonLinesRecordReaderBenchmarks
{
    private const string NumberColumn = "number";
    private const string StringColumn = "string";
    private const string ObjectColumn = "object";
    private const string ArrayColumn = "array";

    // Covers each token type because their parsing paths are independently allocation-sensitive.
    private const string JsonLine =
        """{"number":1.50,"string":"hello","object":{"a":1},"array":[1,2,3]}""";

    private string _tempFilePath = string.Empty;
    private BareJsonLinesRecordReader _reader;

    /// <summary>
    /// Warms each parsing path so allocation measurements exclude one-time reader and
    /// pooled-buffer initialization costs.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _tempFilePath = Path.GetTempFileName();
        File.WriteAllText(_tempFilePath, JsonLine);

        var rowIndexer = new RowIndexer(_tempFilePath);
        rowIndexer.BuildIndex();
        var rowReader = new RowReader(_tempFilePath);
        var (inputColumnNames, outputSchema) = BuildSchemas();
        _reader = new BareJsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema);
        _ = _reader.MoveNextAsync(default).AsTask().GetAwaiter().GetResult();

        _ = _reader.GetCellData(0).Value.Length;
        _ = _reader.GetCellData(1).Value.Length;
        _ = _reader.GetCellData(2).Value.Length;
        _ = _reader.GetCellData(3).Value.Length;
    }

    /// <inheritdoc/>
    [GlobalCleanup]
    public void Cleanup()
    {
        _reader.Dispose();
        File.Delete(_tempFilePath);
    }

    /// <summary>
    /// Reads a Number cell via GetCellData. Returns the value length as a scalar
    /// since CellData is a ref struct and benchmark methods cannot return ref
    /// struct types.
    /// </summary>
    [Benchmark]
    public int ReadCell_Number() => _reader.GetCellData(0).Value.Length;

    /// <summary>
    /// Reads a String cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_String() => _reader.GetCellData(1).Value.Length;

    /// <summary>
    /// Reads an Object cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_Object() => _reader.GetCellData(2).Value.Length;

    /// <summary>
    /// Reads an Array cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_Array() => _reader.GetCellData(3).Value.Length;

    private static (IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema) BuildSchemas()
    {
        string[] inputColumnNames = [NumberColumn, StringColumn, ObjectColumn, ArrayColumn];
        var outputSchema = new BatchOutputSchema(
            [
                new BatchOutputColumn(NumberColumn, NumberColumn),
                new BatchOutputColumn(StringColumn, StringColumn),
                new BatchOutputColumn(ObjectColumn, ObjectColumn),
                new BatchOutputColumn(ArrayColumn, ArrayColumn),
            ],
            []);
        return (inputColumnNames, outputSchema);
    }
}
