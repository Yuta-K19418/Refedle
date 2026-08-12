using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkAssemblyMarker).Assembly).Run(args);

internal static class BenchmarkAssemblyMarker { }
