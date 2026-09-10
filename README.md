![Refedle](docs/images/refedle-terminal-mark.svg)

Refedle is a TUI-driven data transformation tool for CSV and JSON files, built with .NET 10 and Terminal.Gui v2. It lets you explore a file interactively, apply column-level transformations, and replay them as a recipe against large files from the command line.

Explore a file and save the transformations as a recipe:

![Exploring a CSV and saving a recipe in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/hero.gif)

Then replay that recipe from the command line, no UI:

![Replaying the recipe against a file with refedle apply](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/apply.gif)

## Install

On macOS and Linux, install the latest release with:

```bash
curl -fsSL https://raw.githubusercontent.com/Yuta-K19418/Refedle/main/install.sh | sh
```

This downloads the prebuilt binary for your platform, verifies its SHA-256 checksum against the release's `checksums.txt`, and installs `refedle` into the first writable directory among `$XDG_BIN_HOME`, `$HOME/.local/bin`, and `/usr/local/bin`. It never uses `sudo`.

Then check and update it with:

```bash
refedle version      # print the installed version
refedle update       # replace the binary in place with the latest release
```

`refedle update` is not available on Windows or for development builds; download a new archive manually instead.

For manual downloads, prebuilt binaries by platform, and OS security warnings, see [docs/usage/installation.md](docs/usage/installation.md).

To build and run from source instead, replace `refedle` with `dotnet run --project src/App --` in the examples below.

## Supported Formats

| Format | TUI (Tree) | TUI (Table) | CLI batch (`apply`) |
|---|---|---|---|
| CSV (`.csv`) | — | ✅ | ✅ |
| JSON Lines (`.jsonl`) | ✅ | ✅ | ✅ |
| JSON Array (`.json`) | ✅ | via drill-down only | drill-down recipe only |
| JSON Object (`.json`) | ✅ | via drill-down only | drill-down recipe only |

Any file extension other than those listed above results in a `NotSupportedException`.

**CSV (`.csv`)** — TUI Table view. No Tree view, since CSV rows have no nested structure to drill into.

**JSON Lines (`.jsonl`)** — TUI Tree and Table view (toggle with `t`), plus full-file aggregation drill-down (see [TUI Usage](docs/usage/tui.md)).

**JSON Array (`.json`)** — TUI Tree view, with Table view available only via full-file aggregation drill-down (see [TUI Usage](docs/usage/tui.md)). In CLI batch mode, supported only when the recipe is drill-down-scoped (see [CLI Batch Usage](docs/usage/cli.md)).

**JSON Object (`.json`)** — TUI Tree view, with Table view available only via single-node drill-down (see [TUI Usage](docs/usage/tui.md)). In CLI batch mode, supported only when the recipe is drill-down-scoped (see [CLI Batch Usage](docs/usage/cli.md)).

## Usage

```bash
refedle [--file <path>] [--recipe <path.yaml>]
```

Full key bindings and action details: [docs/usage/tui.md](docs/usage/tui.md)

### What you can do

**Rename** a column:

![Renaming a column in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/rename.gif)

**Delete** a column, or **cast** its values to a different type (text, whole number, floating point, etc.).

**Filter** rows by a column condition:

![Filtering rows by a column condition in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/filter.gif)

**Fill** a column with a fixed value — useful for anonymization or masking:

![Masking a column with Fill in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/fill.gif)

**Format timestamp** columns into a different date/time format:

![Reformatting a timestamp column in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/format-timestamp.gif)

**Drill down** into nested JSON — pick a path and it's aggregated into a table:

![Single-node drill-down on a JSON Object in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/drilldown-node.gif)

![Full-file aggregation drill-down on a JSON array in the Refedle TUI](https://raw.githubusercontent.com/Yuta-K19418/Refedle-Assets/main/images/drilldown-aggregate.gif)

See [docs/usage/drilldown.md](docs/usage/drilldown.md) for exact behavior per node type.

**Save a recipe** (`s`) to replay every action above against other files from the CLI — see [docs/usage/recipes.md](docs/usage/recipes.md).

## CLI Batch Usage

```bash
refedle apply --input <input> --recipe <recipe.yaml> --output <output>
```

Replays a saved recipe against a file, no UI. Add `--dry-run` to preview the plan without writing output.

Supported input/output combinations, drill-down recipe rules, and `--dry-run` details: [docs/usage/cli.md](docs/usage/cli.md)

## Project Structure

```
src/
  App/               TUI (Terminal.Gui v2) and CLI entry point (Program.cs, Cli/)
  Engine/            File I/O (mmap-backed), schema scanning, filtering, actions, recipe (de)serialization
  Generators/        Roslyn incremental source generator for format-agnostic dispatch
tests/
  Refedle.Tests/     Unit tests
  Refedle.E2ETests/  End-to-end tests (real refedle binary, real TUI)
benchmarks/
  Refedle.Benchmarks/  BenchmarkDotNet performance benchmarks
docs/                Design documents and usage guides
```

## Implementation Notes

- File reads use `System.IO.MemoryMappedFiles` with `ArrayPool<byte>` buffer reuse; CSV parsing is backed by the [Sep](https://github.com/nietras/Sep) library. There is no SIMD/vectorized scanning code in the engine at this time.
- Recipe YAML is a hand-written, AOT-safe reader/writer (no YamlDotNet or reflection-based serialization).
- Error handling favors a `Result`/`Result<T>` return type over exceptions on expected failure paths.
- Both `App` and `Engine` are configured for Native AOT (`PublishAot=true` / `IsAotCompatible=true`) with `TreatWarningsAsErrors` enabled.

## Requirements

- .NET SDK 10.0.201+ (see [global.json](global.json))

## Build & Test

```bash
dotnet build
dotnet test
```

## Acknowledgements

Built with [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) and [Sep](https://github.com/nietras/Sep), both MIT licensed. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for full license texts.

## License

[MIT](LICENSE)
