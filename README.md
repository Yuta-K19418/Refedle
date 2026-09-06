![Refedle](docs/images/refedle-terminal-mark.svg)

Refedle is a TUI-driven data transformation tool for CSV and JSON files, built with .NET 10 and Terminal.Gui v2. It lets you explore a file interactively, apply column-level transformations, and replay them as a recipe against large files from the command line.

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

### Manual download

Prebuilt binaries (no .NET SDK required) are published on the [Releases page](https://github.com/Yuta-K19418/Refedle/releases) for:

| OS | Architecture |
|---|---|
| Windows | x64 |
| macOS | Apple Silicon (arm64) |
| Linux | x64 |
| Linux | arm64 |

macOS on Intel (`osx-x64`) is not supported; build from source with `dotnet publish src/App/Refedle.App.csproj -r osx-x64 -c Release`.

Download the archive for your platform and `checksums.txt`, verify, extract, and put `refedle` on your `PATH` (Linux x64 example):

```bash
tag=v0.3.0
base=https://github.com/Yuta-K19418/Refedle/releases/download/$tag
curl -fLO $base/refedle-$tag-linux-x64.tar.gz
curl -fLO $base/checksums.txt
sha256sum -c --ignore-missing checksums.txt
tar -xzf refedle-$tag-linux-x64.tar.gz
install -Dm755 refedle-$tag-linux-x64/refedle ~/.local/bin/refedle
```

On Windows, download `refedle-<tag>-win-x64.zip`, extract it, and move `refedle.exe` onto your `PATH`.

The binaries are unsigned, so the OS may block them on first launch:

- **macOS**: Gatekeeper quarantines downloaded files. Run `xattr -d com.apple.quarantine refedle` before launching, or allow it via System Settings → Privacy & Security.
- **Windows**: SmartScreen may warn about an unrecognized app. Click "More info" → "Run anyway".

Run the binary directly as `./refedle [--file <path>] [--recipe <path.yaml>]` (or `refedle.exe` on Windows). In the examples below, `dotnet run --project src/App --` can be replaced with `./refedle` when using a downloaded binary instead of building from source.

To build and run from source instead, see [TUI Usage](#tui-usage) below.

## Supported Formats

| Format | TUI (Tree) | TUI (Table) | CLI batch (`apply`) |
|---|---|---|---|
| CSV (`.csv`) | — | ✅ | ✅ |
| JSON Lines (`.jsonl`) | ✅ | ✅ | ✅ |
| JSON Array (`.json`) | ✅ | via drill-down only | drill-down recipe only |
| JSON Object (`.json`) | ✅ | via drill-down only | drill-down recipe only |

Any file extension other than those listed above results in a `NotSupportedException`.

**CSV (`.csv`)** — TUI Table view. No Tree view, since CSV rows have no nested structure to drill into.

**JSON Lines (`.jsonl`)** — TUI Tree and Table view (toggle with `t`), plus full-file aggregation drill-down (see [TUI Usage](#tui-usage)).

**JSON Array (`.json`)** — TUI Tree view, with Table view available only via full-file aggregation drill-down (see [TUI Usage](#tui-usage)). In CLI batch mode, supported only when the recipe is drill-down-scoped (see [CLI Batch Usage](#cli-batch-usage)).

**JSON Object (`.json`)** — TUI Tree view, with Table view available only via single-node drill-down (see [TUI Usage](#tui-usage)). In CLI batch mode, supported only when the recipe is drill-down-scoped (see [CLI Batch Usage](#cli-batch-usage)).

## TUI Usage

```bash
dotnet run --project src/App -- [--file <path>] [--recipe <path.yaml>]
```

Key bindings:

| Key | Action |
|---|---|
| `o` | Open file |
| `s` | Save recipe |
| `t` | Toggle Tree/Table view (JSON Lines only) |
| `x` | Action menu (Column/Row Actions, Drill-down) |
| `c` | Clear action stack |
| `Backspace` | Back from drill-down |
| `?` | Help |
| `q` | Quit (confirms if there are unsaved actions) |

### Column/Row Actions

Available from the action menu (`x`):

| View | Column/Row Actions |
|---|---|
| CSV (Table) | ✅ |
| JSON Lines (Table) | ✅ |
| Table from drill-down (any format) | planned |

**Rename** — renames a column.

Before:

| nm | age |
|---|---|
| Alice | 30 |

After (renamed `nm` to `name`):

| name | age |
|---|---|
| Alice | 30 |

**Delete** — removes a column from the dataset.

Before:

| name | age |
|---|---|
| Alice | 30 |

After (deleted `age`):

| name |
|---|
| Alice |

**Cast** — converts a column's values to a different type (text, whole number, floating point, etc.).

Before:

| age |
|---|
| "30" |

After (cast `age` from text to whole number):

| age |
|---|
| 30 |

**Filter** — keeps only the rows where a column matches a condition (equals, not-equals, greater/less than, etc.). Multiple filters combine with AND.

Before:

| age |
|---|
| 30 |
| 20 |

After (filtered `age > 25`):

| age |
|---|
| 30 |

**Fill** — overwrites every value in a column with a fixed value; useful for anonymization, masking, or bulk initialization.

Before:

| email |
|---|
| alice@example.com |
| bob@example.com |
| carol@example.com |

After (filled with `"REDACTED"`):

| email |
|---|
| REDACTED |
| REDACTED |
| REDACTED |

**Format Timestamp** — reformats a Timestamp column's string values into a different date/time format.

Before:

| created_at |
|---|
| 2024-01-15T09:30:00Z |
| 2024-03-02T14:05:00Z |
| 2024-06-21T08:45:00Z |

After (formatted as `yyyy-MM-dd`):

| created_at |
|---|
| 2024-01-15 |
| 2024-03-02 |
| 2024-06-21 |

### Drill-down

Also available from the action menu (`x`), when the current view is in Tree mode. Two modes exist:

| View | Drill-down type |
|---|---|
| JSON Lines (Tree) | Full-aggregation |
| JSON Array (Tree) | Full-aggregation |
| JSON Object (Tree) | Single-node |

**Single-node drill-down** — turns the selected node itself into a table (JSON Object only — there's a single record to explore). The selected node must be a non-empty array of objects; selecting an object, a scalar (a plain value — not an object or array), or an array with non-object elements fails with an error.

Example — a JSON Object file:

```json
{
  "user": "alice",
  "orders": [
    { "id": 1, "item": "Book", "price": 12.5 },
    { "id": 2, "item": "Pen", "price": 1.2 }
  ]
}
```

Path: `orders`

Drilling down produces:

| id | item | price |
|---|---|---|
| 1 | Book | 12.5 |
| 2 | Pen | 1.2 |

**Full-file aggregation drill-down** — scans the entire file and aggregates the selected path across every record into a table (JSON Lines/Array).

<details>
<summary>Behavior by selected node type</summary>

The shape of the resulting table depends on what the selected path resolves to in each record:

- **Object** — one row per record, using the object's keys as columns.
- **Array** — one row per element; object elements become row columns, primitive elements become a single `value` column (always typed as Text). Selecting a specific array element (e.g. `tags[0]`) produces the same result as selecting the array itself — the whole array is always expanded.
- **Scalar** (a plain value — not an object or array) — one row with a single column (named after the path's last key), always typed as Text regardless of the actual value.

</details>

Example — a JSON Lines file (one record per line):

```json
{"user": "alice", "cart": {"orders": [{"id": 1, "item": "Book"}]}}
{"user": "bob", "cart": {"orders": [{"id": 2, "item": "Pen"}, {"id": 3, "item": "Mug"}]}}
```

Path: `cart > orders`

Drilling down scans every line and aggregates all matching arrays into one table:

| id | item |
|---|---|
| 1 | Book |
| 2 | Pen |
| 3 | Mug |

### Recipes

Pressing `s` saves the current action stack as a `.yaml` recipe, named after the source file.

Example — `people.csv`:

| nm | age | email |
|---|---|---|
| Alice | 30 | alice@example.com |
| Bob | 20 | bob@example.com |

After renaming `nm` → `name`, filling `email` with `"REDACTED"`, and filtering `age > 25`:

| name | age | email |
|---|---|---|
| Alice | 30 | REDACTED |

Pressing `s` at this point produces `people.yaml`:

```yaml
name: "people"
lastModified: 2026-07-26T12:34:56.0000000+00:00
actions:
  - type: Rename
    oldName: "nm"
    newName: "name"
  - type: Fill
    columnName: "email"
    value: "REDACTED"
  - type: Filter
    columnName: "age"
    operator: GreaterThan
    comparisonType: Number
    value: "25"
```

Recipes can then be replayed against other files via [CLI Batch Usage](#cli-batch-usage), without opening the UI.

## CLI Batch Usage

| Input \ Output | CSV | JSON Lines | JSON (`.json`) |
|---|---|---|---|
| CSV | ✅ | ✅ | ✅ |
| JSON Lines | ✅ | ✅ | ✅ |
| JSON Array | ✅ ¹ | ✅ ¹ | ✅ ¹ |
| JSON Object | ✅ ¹ | ✅ ¹ | ✅ ¹ |

¹ JSON Array / JSON Object input requires a drill-down-scoped recipe — the recipe's `drillDownKeyPath` selects the table to transform. Bare (non-drill-down) batch mode for these formats is out of scope. JSON Lines input works with or without a drill-down scope.

```bash
dotnet run --project src/App -- apply --input <input> --recipe <recipe.yaml> --output <output>
```

`.json` output is always a JSON array (`[{...}, ...]`), regardless of row count or input format.

Format dispatch (reader → transform → writer) is resolved at compile time via a source generator (`src/Generators/FormatDispatcherGenerator.cs`), not reflection.

## Project Structure

```
src/
  App/         TUI (Terminal.Gui v2) and CLI entry point (Program.cs, Cli/)
  Engine/      File I/O (mmap-backed), schema scanning, filtering, actions, recipe (de)serialization
  Generators/  Roslyn incremental source generator for format-agnostic dispatch
tests/
  Refedle.Tests/
docs/          Design documents
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
