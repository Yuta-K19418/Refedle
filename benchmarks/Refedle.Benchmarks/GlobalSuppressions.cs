using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Benchmarks.App.Cli.JsonLinesRecordReaderBenchmarks.Setup",
    Justification = "RowReader ownership is transferred to BareJsonLinesRecordReader; Cleanup disposes the reader.")]

[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.App.Cli.JsonLinesRecordReaderBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.Csv.DataRowIndexerBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.Json.JsonObjectCellExtractorBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.JsonArray.ElementByteCacheBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.JsonArray.RowIndexerBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.JsonLines.RowByteCacheBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.JsonLines.RowIndexerBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.JsonObject.TopLevelScannerBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Scope = "type",
    Target = "~T:Refedle.Benchmarks.Engine.IO.MmapServiceBenchmarks",
    Justification = "BenchmarkDotNet generates a derived type in a separate assembly.")]
