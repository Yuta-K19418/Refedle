using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO;

namespace Refedle.Benchmarks.Engine.IO;

/// <summary>
/// Benchmarks mapped-file reads.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class MmapServiceBenchmarks : IDisposable
{
    private readonly string _testFilePath;
    private readonly MmapService _service;
    private const int FileSize = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Initializes benchmark data.
    /// </summary>
    public MmapServiceBenchmarks()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid()}.dat");
        var data = new byte[FileSize];
        Random.Shared.NextBytes(data);
        File.WriteAllBytes(_testFilePath, data);
        _service = MmapService.Open(_testFilePath).Value;
    }

    /// <summary>
    /// Reads a small chunk.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Read_SmallChunk()
    {
        Span<byte> buffer = stackalloc byte[1024];
        _service.Read(0, buffer);
        var sum = 0;
        foreach (var b in buffer)
        {
            sum += b;
        }

        return sum;
    }

    /// <summary>
    /// Reads a medium chunk.
    /// </summary>
    [Benchmark]
    public int Read_MediumChunk()
    {
        Span<byte> buffer = stackalloc byte[64 * 1024];
        _service.Read(0, buffer);
        var sum = 0;
        foreach (var b in buffer)
        {
            sum += b;
        }

        return sum;
    }

    /// <summary>
    /// Reads a large chunk.
    /// </summary>
    [Benchmark]
    public int Read_LargeChunk()
    {
        var buffer = new byte[1024 * 1024];
        _service.Read(0, buffer);
        var sum = 0;
        foreach (var b in buffer)
        {
            sum += b;
        }

        return sum;
    }

    /// <summary>
    /// Reads with validation.
    /// </summary>
    [Benchmark]
    public int TryRead_WithValidation()
    {
        Span<byte> buffer = stackalloc byte[1024];
        var (success, _) = _service.TryRead(0, buffer);
        if (success)
        {
            var sum = 0;
            foreach (var b in buffer)
            {
                sum += b;
            }

            return sum;
        }

        return 0;
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
        if (!disposing)
        {
            return;
        }

        _service.Dispose();
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }
}
