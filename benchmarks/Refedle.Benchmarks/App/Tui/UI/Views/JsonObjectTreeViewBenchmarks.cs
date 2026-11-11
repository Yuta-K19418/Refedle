using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonObject;

namespace Refedle.Benchmarks.App.Tui.UI.Views;

/// <summary>
/// Builds the fixed in-memory input (keys <c>key{i}</c>, numeric values, index order) for
/// <see cref="JsonObjectTreeView"/> benchmarks, and logs the canonical JSON byte count and
/// SHA-256 hash so every run records exactly which input it measured.
/// </summary>
internal static class JsonObjectTreeViewBenchmarkInput
{
    /// <summary>
    /// Builds entries with keys <c>key{i}</c> and values <c>i</c> rendered as JSON number bytes.
    /// </summary>
    internal static JsonObjectEntry[] BuildEntries(int keyCount) =>
        [
            .. Enumerable
                .Range(0, keyCount)
                .Select(i => new JsonObjectEntry(
                    string.Create(CultureInfo.InvariantCulture, $"key{i}"),
                    Encoding.UTF8.GetBytes(i.ToString(CultureInfo.InvariantCulture)))),
        ];

    /// <summary>
    /// Prints the input size/hash and the runtime GC configuration once per case, outside the measured region.
    /// </summary>
    internal static void LogInput(int keyCount, JsonObjectEntry[] entries)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < keyCount; i++)
        {
            builder.Append(i == 0 ? "{\"" : ",\"");
            builder.Append(string.Create(CultureInfo.InvariantCulture, $"key{i}\":{i}"));
        }

        builder.Append('}');
        var json = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(json));
        Console.WriteLine(
            $"// INPUT keys={keyCount} entries={entries.Length} jsonBytes={json.Length} sha256={hash}");
        Console.WriteLine(
            $"// ENV runtime={RuntimeInformation.FrameworkDescription} serverGC={GCSettings.IsServerGC} gcLatency={GCSettings.LatencyMode}");
    }
}

/// <summary>
/// Base class for the 500/1,000/2,000-key benchmarks: creates exactly one tree in a single-shot run
/// (RunStrategy.Monitoring, 1 warmup iteration + 1 measured iteration, 1 invocation each);
/// the view is disposed in iteration cleanup, outside the measured region.
/// </summary>
public abstract class JsonObjectTreeViewSingleBenchmarksBase
{
    /// <summary>Gets the number of top-level keys of the input object (one case per concrete class).</summary>
    private protected abstract int KeyCount { get; }

    private JsonObjectEntry[] _entries = [];
    private JsonObjectTreeView? _view;

    /// <summary>Builds the in-memory entries.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _entries = JsonObjectTreeViewBenchmarkInput.BuildEntries(KeyCount);
        JsonObjectTreeViewBenchmarkInput.LogInput(KeyCount, _entries);
    }

    /// <summary>Guards against overwriting a view a failed iteration left behind.</summary>
    [IterationSetup]
    public void VerifySlotEmpty()
    {
        if (_view is not null)
        {
            throw new InvalidOperationException("View slot is not empty before the iteration.");
        }
    }

    /// <summary>Creates one tree as the measured work.</summary>
    private protected void CreateTreeCore()
    {
        _view = JsonObjectTreeView.Create(_entries, static () => { }, static _ => { });
    }

    /// <summary>Disposes the view created by the iteration, outside the measured region.</summary>
    [IterationCleanup]
    public void DisposeView()
    {
        _view?.Dispose();
        _view = null;
    }

    /// <summary>Disposes the view a failed iteration left behind.</summary>
    [GlobalCleanup]
    public void Cleanup() => DisposeView();
}

/// <summary>500-key case: .NET 10, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.Net11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys500NetBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 500;

    /// <summary>Creates one tree for 500 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 500 keys")]
    public void CreateTree() => CreateTreeCore();
}

/// <summary>500-key case: Native AOT, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.NativeAot11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys500AotBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 500;

    /// <summary>Creates one tree for 500 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 500 keys")]
    public void CreateTree() => CreateTreeCore();
}

/// <summary>1,000-key case: .NET 10, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.Net11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys1000NetBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 1000;

    /// <summary>Creates one tree for 1000 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 1000 keys")]
    public void CreateTree() => CreateTreeCore();
}

/// <summary>1,000-key case: Native AOT, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.NativeAot11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys1000AotBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 1000;

    /// <summary>Creates one tree for 1000 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 1000 keys")]
    public void CreateTree() => CreateTreeCore();
}

/// <summary>2,000-key case: .NET 10, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.Net11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys2000NetBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 2000;

    /// <summary>Creates one tree for 2000 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 2000 keys")]
    public void CreateTree() => CreateTreeCore();
}

/// <summary>2,000-key case: Native AOT, single-shot.</summary>
[MemoryDiagnoser]
[SimpleJob(
    runtimeMoniker: RuntimeMoniker.NativeAot11_0,
    runStrategy: RunStrategy.Monitoring,
    warmupCount: 1,
    iterationCount: 1,
    invocationCount: 1)]
public class JsonObjectTreeViewSingleKeys2000AotBenchmarks : JsonObjectTreeViewSingleBenchmarksBase
{
    /// <inheritdoc/>
    private protected override int KeyCount => 2000;

    /// <summary>Creates one tree for 2000 keys.</summary>
    [Benchmark(Description = "Create 1 tree for 2000 keys")]
    public void CreateTree() => CreateTreeCore();
}
