using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO.Json;

namespace Refedle.Benchmarks.Engine.IO.Json;

/// <summary>
/// Benchmarks for JsonObjectJsonObjectCellExtractor.ExtractCell.
/// Validates zero-allocation per cell access and compares
/// single-column extraction vs full-row extraction.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class JsonObjectCellExtractorBenchmarks
{
    public JsonObjectCellExtractorBenchmarks()
    {
        _allColumns = [_columnId, _columnName, _columnAge, _columnActive, _columnScore];
    }

    // Representative 5-column JSON Lines row (mirrors typical JSONL data)
    private readonly byte[] _jsonLine =
        "{\"id\":1,\"name\":\"Alice\",\"age\":30,\"active\":true,\"score\":9.5}"u8.ToArray();

    // Pre-encoded column name bytes — mirrors JsonLinesTableSource._columnNameUtf8
    private readonly byte[] _columnId = "id"u8.ToArray();
    private readonly byte[] _columnName = "name"u8.ToArray();
    private readonly byte[] _columnAge = "age"u8.ToArray();
    private readonly byte[] _columnActive = "active"u8.ToArray();
    private readonly byte[] _columnScore = "score"u8.ToArray();

    private readonly byte[][] _allColumns;

    /// <summary>
    /// Extracts the first column — best-case scan (found immediately).
    /// </summary>
    [Benchmark]
    public string ExtractCell_FirstColumn() => JsonObjectCellExtractor.ExtractCell(_jsonLine, _columnId);

    /// <summary>
    /// Extracts the last column — worst-case scan (traverses all preceding keys).
    /// </summary>
    [Benchmark]
    public string ExtractCell_LastColumn() => JsonObjectCellExtractor.ExtractCell(_jsonLine, _columnScore);

    /// <summary>
    /// Extracts all columns — simulates full-row rendering in TableView.
    /// </summary>
    [Benchmark]
    public void ExtractCell_AllColumns()
    {
        foreach (var column in _allColumns)
        {
            _ = JsonObjectCellExtractor.ExtractCell(_jsonLine, column);
        }
    }
}
