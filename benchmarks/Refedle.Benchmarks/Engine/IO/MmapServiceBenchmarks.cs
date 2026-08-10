using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Refedle.Engine.IO;

namespace Refedle.Benchmarks.Engine.IO;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[SimpleJob(RuntimeMoniker.NativeAot10_0)]
public class MmapServiceBenchmarks : IDisposable
{
    private readonly string _testFilePath;
    private readonly MmapService _service;
    private const int FileSize = 10 * 1024 * 1024; // 10MB

    public MmapServiceBenchmarks()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid()}.dat");
        var data = new byte[FileSize];
        Random.Shared.NextBytes(data);
        File.WriteAllBytes(_testFilePath, data);
        _service = MmapService.Open(_testFilePath).Value;
    }

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

    public void Dispose()
    {
        _service.Dispose();
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }
}
