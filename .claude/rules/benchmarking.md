---
paths:
  - "benchmarks/**/*.cs"
---

# Benchmarking

## Framework
- Use **BenchmarkDotNet** for all performance measurements
- Benchmarks live in their own project, **`benchmarks/Refedle.Benchmarks`** (an executable, not a test project — `dotnet test` does not run it)

## When Benchmarks Are Mandatory
- Core engine / hot path components **must** have benchmarks:
  - `MmapService`
  - `RowIndexer`
  - `Parser`
  - Other hot path (data processing) components
- Prioritize covering engine logic over UI state

## Directory Placement
- Benchmark classes **mirror the directory hierarchy** of the code they measure
- Example: a benchmark for `src/Engine/IO/Csv/DataRowIndexer.cs` belongs in `benchmarks/Refedle.Benchmarks/Engine/IO/Csv/`

## Naming Conventions
- **Class**: `[ClassName]Benchmarks` — e.g. `DataRowIndexerBenchmarks`, `MmapServiceBenchmarks`
- **Method**: an action-first verb phrase naming the operation and its input size — e.g. `IndexSmallCsv`, `GetCheckpointLargeFile`
- Give each `[Benchmark]` a `Description` when the method name alone does not convey the input scale

## Native AOT Toolchain
- Benchmarks **must** be executed under the `NativeAot` toolchain so measurements reflect real-world Native AOT deployment
- Standard job attributes on a benchmark class:
  ```csharp
  [SimpleJob(RuntimeMoniker.Net10_0)]
  [SimpleJob(RuntimeMoniker.NativeAot10_0)]
  [MemoryDiagnoser]
  public class DataRowIndexerBenchmarks
  ```

## Control Flow
- Unlike unit tests, `[Benchmark]` methods **may** use `for` loops to perform the work being measured

## Resource Cleanup
- Benchmarks that create external resources (temp files, streams, etc.) must release them — implement `IDisposable` / `IAsyncDisposable` and delete what the constructor or `[GlobalSetup]` created

## Running
- BenchmarkDotNet requires a **Release** build:
  ```
  dotnet run -c Release --project benchmarks/Refedle.Benchmarks -- --filter '*DataRowIndexer*'
  ```
- `--filter '*'` runs every benchmark; the bare run with no `--filter` prompts interactively
