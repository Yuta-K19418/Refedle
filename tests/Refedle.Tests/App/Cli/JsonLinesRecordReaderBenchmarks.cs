using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

/// <summary>
/// Benchmarks for JsonLinesRecordReader.GetCellData. Validates zero-allocation
/// per cell access across representative token types (Number, String, Object, Array).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.NativeAot80)]
public sealed class JsonLinesRecordReaderBenchmarks
{
    // Skeleton placeholders: read/written for the first time in Setup/Cleanup
    // during Step 2, at which point these suppressions are removed.
#pragma warning disable CA1823, IDE0044, IDE0052
    private string _tempFilePath = string.Empty;
#pragma warning restore CA1823, IDE0044, IDE0052

#pragma warning disable CS0169, CA1823, IDE0051, CS0649
    private JsonLinesRecordReader _reader;
#pragma warning restore CS0169, CA1823, IDE0051, CS0649

    /// <summary>
    /// Writes a representative JSON Lines row, builds the reader, advances to the
    /// first row, and warms up the pooled buffer so measured Allocated reflects
    /// steady-state per-cell cost rather than first-call setup.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Disposes the reader (returning the rented buffer to ArrayPool) and deletes
    /// the temporary input file.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Reads a Number cell via GetCellData. Returns the value length as a scalar
    /// since CellData is a ref struct and benchmark methods cannot return ref
    /// struct types.
    /// </summary>
    [Benchmark]
    public int ReadCell_Number() => throw new NotImplementedException();

    /// <summary>
    /// Reads a String cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_String() => throw new NotImplementedException();

    /// <summary>
    /// Reads an Object cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_Object() => throw new NotImplementedException();

    /// <summary>
    /// Reads an Array cell via GetCellData. Returns the value length as a scalar.
    /// </summary>
    [Benchmark]
    public int ReadCell_Array() => throw new NotImplementedException();
}
