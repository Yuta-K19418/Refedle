using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Refedle.Benchmarks.App.Cli.IO.Json.JsonLinesRecordReaderBenchmarks.Setup",
    Justification = "RowReader ownership is transferred to BareJsonLinesRecordReader; Cleanup disposes the reader.")]
