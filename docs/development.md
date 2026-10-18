# Development

Coding, testing, and commit conventions: [development_guidelines.md](development_guidelines.md)

## Setup & Common Commands
- **Requirements**: .NET SDK 10.0.201+ (see [global.json](../global.json))
- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Format**: `dotnet format`
- **Run from source**: Replace `refedle` with `dotnet run --project src/App --` in the [usage guides](usage/) examples.

## Project Structure
```
src/
  App/               TUI (Terminal.Gui v2) and CLI entry point (Program.cs, Cli/)
  Engine/            File I/O, schema scanning, filtering, actions, recipe (de)serialization
  Generators/        Roslyn incremental source generator for format-agnostic dispatch
tests/
  Refedle.Tests/     Unit tests
  Refedle.E2ETests/  End-to-end tests (real refedle binary, real TUI)
benchmarks/
  Refedle.Benchmarks/  BenchmarkDotNet performance benchmarks
docs/                Design documents and usage guides
```

## Implementation Notes
- Row indexers scan files sequentially on a background thread using positional reads (`RandomAccess.Read`) into `ArrayPool<byte>` buffers. JSON Lines and JSON Array rows are then fetched by random access through `System.IO.MemoryMappedFiles`; CSV rows are read by seeking to the nearest checkpoint and parsing with [Sep](https://github.com/nietras/Sep).
- Recipe YAML is a hand-written, AOT-safe reader/writer (no YamlDotNet or reflection-based serialization).
- Error handling favors a `Result`/`Result<T>` return type over exceptions on expected failure paths.
- Both `App` and `Engine` are configured for Native AOT (`PublishAot=true` / `IsAotCompatible=true`) with `TreatWarningsAsErrors` enabled.
