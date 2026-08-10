using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Benchmarks.App.Cli.JsonLinesRecordReaderBenchmarks.Setup",
    Justification = "RowReader ownership is transferred to JsonLinesRecordReader; Cleanup disposes the reader.")]
