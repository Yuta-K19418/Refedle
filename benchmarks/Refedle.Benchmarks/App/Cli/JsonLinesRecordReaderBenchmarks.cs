using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Benchmarks.App.Cli;

/// <summary>
/// Benchmarks for BareJsonLinesRecordReader.GetCellData. Measures per-cell managed
/// allocation across representative token types (Number, String, Object, Array);
/// zero allocation is the target once GetCellData is backed by a pooled buffer.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class JsonLinesRecordReaderBenchmarks
{
    private const string NumberColumn = "number";
    private const string StringColumn = "string";
    private const string ObjectColumn = "object";
    private const string ArrayColumn = "array";

    // A representative single JSON Lines row exercising the four token types GetCellData
    // branches on. Phase A: each branch allocates a string per cell; this benchmark
    // visualizes that per-call cost (it does not reach zero until Phase B).
    private const string JsonLine =
        """{"number":1.50,"string":"hello","object":{"a":1},"array":[1,2,3]}""";

    private string _tempFilePath = string.Empty;
    private BareJsonLinesRecordReader _reader;

    /// <summary>
    /// Writes a representative JSON Lines row, builds the reader, advances to the
    /// first row, and warms up each token-type branch so measured Allocated reflects
    /// steady-state per-cell cost rather than first-call setup. After Phase B this
    /// warm-up also forces the initial pooled-buffer Reserve.
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

        // Warm up each branch in Setup so the measured Allocated excludes first-call
        // cost: JIT today, and (post-Phase-B) the one-time pooled-buffer rent.
        _ = _reader.GetCellData(0).Value.Length;
        _ = _reader.GetCellData(1).Value.Length;
        _ = _reader.GetCellData(2).Value.Length;
        _ = _reader.GetCellData(3).Value.Length;
    }

    /// <summary>
    /// Disposes the reader (returning any rented buffer to ArrayPool after Phase B) and
    /// deletes the temporary input file.
    /// </summary>
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
