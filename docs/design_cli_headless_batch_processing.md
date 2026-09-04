# Design: CLI Headless Mode for Batch Processing

## Requirements

Apply a saved recipe (YAML) to an input file without launching the TUI,
streaming the transformed output to a new file.

**Invocation:**

```
refedle apply --input <input> --recipe <recipe.yaml> --output <output>
```

**Supported input/output formats:**
- CSV (`.csv`) — input and output
- JSON Lines (`.jsonl`) — input and output

Output format is inferred from the `--output` extension; it does not need to
match the input format (e.g., `.csv` input → `.jsonl` output is valid).

**Supported actions (from existing `MorphAction` types):**
- `RenameColumnAction` — rename a column in the output
- `DeleteColumnAction` — omit a column from the output entirely
- `CastColumnAction`  — affects filter type resolution only; output values remain strings
- `FilterAction`      — skip rows that do not satisfy the predicate (AND semantics)

**Exit codes:**
- `0` — success
- `1` — any error (bad args, recipe load failure, I/O error)

---

## Files Changed

### New — Engine

| File | Purpose |
|------|---------|
| `src/Engine/BatchOutputSchema.cs` | Immutable, format-agnostic output plan: column name mapping + resolved filter specs |
| `src/Engine/ActionApplier.cs` | Pure, stateless logic that translates an action stack into a `BatchOutputSchema` |

### New — App

| File | Purpose |
|------|---------|
| `src/App/Cli/Arguments.cs` | Record holding validated CLI argument values |
| `src/App/Cli/ArgumentParser.cs` | Parses `string[]` into `Result<Arguments>` |
| `src/App/Cli/Runner.cs` | Orchestrates recipe load → schema detection → transform → write for both formats |

### Modified

| File | Change |
|------|--------|
| `src/App/Program.cs` | Branch to `Runner` (via `ApplyRunner`) when the first argument is `apply`; otherwise run TUI |

### New — Tests

| File | Covers |
|------|--------|
| `tests/Refedle.Tests/Engine/ActionApplierTests.cs` | `ActionApplier.BuildOutputSchema` under all action combinations |
| `tests/Refedle.Tests/App/Cli/ArgumentParserTests.cs` | All valid and invalid argument scenarios |
| `tests/Refedle.Tests/App/Cli/RunnerTests.cs` | End-to-end: real temp files for both CSV and JSON Lines, verifies output content |

---

## Implementation Approach

### 1. Argument Parsing (`ArgumentParser`)

Accepts the named-flag style `--key value`. Required flags: `--input`,
`--recipe`, `--output`. Unknown flags are rejected. The result
is an `Arguments` record (all fields non-nullable `string`).

```
Arguments { InputFile, RecipeFile, OutputFile }
```

### 2. Format-Agnostic Output Schema (`ActionApplier`)

`ActionApplier.BuildOutputSchema(TableSchema schema, IReadOnlyList<MorphAction> actions)`
returns a `BatchOutputSchema`:

```
BatchOutputSchema
  Columns : IReadOnlyList<BatchOutputColumn>  // (SourceName, OutputName) pairs
  Filters : IReadOnlyList<FilterSpec>          // resolved filter specs
```

`BatchOutputColumn(string SourceName, string OutputName)` uses column *names*
(not indices) so it is format-agnostic — CSV writers resolve the index from
the name; JSON Lines writers use the name directly as a JSON key.

**Note on FilterSpec and column indices:**
The `FilterSpec` type is reused from the existing filtering infrastructure and
contains `SourceColumnIndex` (an `int`). For batch processing, this index always
refers to the column index in the original input schema (before any actions are applied).
The `ActionApplier` resolves column names to their original source indices when
building `FilterSpec`s, and filter evaluation uses these indices to extract values
from input rows. This design allows reuse of the existing `FilterSpec` type without
modification.

The applier iterates through the action stack in order, maintaining a mutable
"working column list" (initialized from the input schema) and a filter list:

- **RenameColumnAction**: if the named column exists, update its `OutputName`; otherwise skip silently.
- **DeleteColumnAction**: if the named column exists, remove it from the working list; otherwise skip silently.
- **CastColumnAction**: update the `ColumnType` in the working list so that subsequent
  `FilterAction`s use the post-cast type; no effect on output name or inclusion.
- **FilterAction**: look up the column in the current working list, resolve its
  `ColumnType`, and append a `FilterSpec`. If the column does not exist (was deleted
  earlier), skip silently.

This single-pass approach correctly handles action ordering (e.g., a
`CastColumnAction` followed by a `FilterAction` on the same column uses the
cast type for comparison).

### 3. Streaming Transform (`Runner`)

```
Runner.RunAsync(Arguments args, CancellationToken ct) → Task<int>
```

**Shared steps (both formats):**

1. **Load recipe** via `RecipeManager.LoadAsync`.
2. **Detect input format** by extension (`.csv` or `.jsonl`; reject others).
3. **Detect output format** by extension (`.csv` or `.jsonl`; reject others).
4. **Scan schema** — call `IncrementalSchemaScanner.InitialScanAsync()` (first 200
   rows/lines) to obtain column names and inferred types for filter evaluation.
5. **Build output schema** — call `ActionApplier.BuildOutputSchema`.

**CSV output path (`WriteCsvAsync`):**

- Open input with Sep reader (sequential, no header mode so the raw header row
  is intercepted to build the source-name-to-index map).
- Write the transformed header row to the output `StreamWriter`.
- For each data row:
  - Evaluate all `FilterSpec`s via `FilterEvaluator.EvaluateFilter` (using
    `ReadOnlySpan<char>`); skip the row if any spec fails (AND semantics).
  - Project the passing row through `BatchOutputSchema.Columns` (resolve source
    index from name, select the cell).
  - Write the projected row with RFC 4180 CSV escaping.

**JSON Lines output path (`WriteJsonLinesAsync`):**

- Build `RowIndexer` (full file scan to record a byte-offset checkpoint every
  1,000 lines), then instantiate `RowReader`.
- Pre-encode each source column name to UTF-8 bytes for use with
  `CellExtractor.ExtractCell` (done once before the row loop).
- Iterate in batches of 1,000 lines: call `RowIndexer.GetCheckPoint(batchStart)`
  to obtain the nearest checkpoint byte offset, then
  `RowReader.ReadLineBytes(byteOffset, rowOffset, 1000)` to get
  `ReadOnlyMemory<byte>` per line — no per-line `string` allocation.
- For each non-empty line:
  - Extract each column's string value via `CellExtractor.ExtractCell`.
  - Evaluate all `FilterSpec`s via `FilterEvaluator.EvaluateFilter`; skip if any fails.
  - Write the projected columns as a JSON object using `Utf8JsonWriter` (one
    object per line, flushed per row, AOT-compatible).

6. **Return exit code** — `0` on success, `1` on any failure; print a human-readable
   error line to `stderr`.

**Memory model:**
- CSV: Sep reader processes one row at a time; `FilterEvaluator` accepts
  `ReadOnlySpan<char>` — no per-row heap allocations on the filter path.
- JSON Lines: `RowReader` returns `ReadOnlyMemory<byte>` per line in batches of
  1,000 — no per-line `string` allocation; live heap is bounded to one batch at a time.
- Both output writers flush per row to avoid buffering entire files in memory.

### 4. Program Entry (`Program.cs`)

```csharp
if (args is ["apply", ..])
{
    return await ApplyRunner.RunAsync(args[1..], ct);
}
// existing TUI path
```

`ApplyRunner` is the composition root for the `apply` subcommand: it parses the
remaining arguments via `ArgumentParser.Parse`, reports parse failures to
`stderr` with `ExitCode.Failure`, and otherwise runs `Runner.RunAsync` with a
production `ConsoleAppLogger`.

---

## Decision Record

### Rationale

**`ActionApplier` in the Engine project (not App):** The logic of translating an
action stack + schema into a column plan and filter specs is pure computation
with no I/O dependency. Placing it in Engine makes it independently testable
and reusable across any future output format or execution context.

**`BatchOutputSchema` uses source *names* not indices:** This makes the schema
format-agnostic. CSV writers derive the column index from the input header
at read time; JSON Lines writers use the name as a JSON key directly. A
name-based contract avoids duplicating the applier for each format.

**Output format is determined by `--output` extension, not `--input`:** This
allows cross-format conversion (e.g., CSV → JSON Lines) at no extra complexity,
which is a natural capability of the transformation pipeline.

**`StreamWriter` with manual RFC 4180 escaping for CSV output:** Sep's writer
surface area for the version in use (`0.12.2`) requires knowing the column
count up-front and is optimized for in-memory round-trips. Manual escaping
keeps the dependency surface minimal and the output predictable.

**Static Polymorphism (Generics with struct constraints) for the pipeline:** 
The transformation pipeline follows a generic structure: `Process<TReader, TWriter>(ref TReader reader, ref TWriter writer)`. By using `struct` constraints (`where T : struct`), we ensure the JIT/AOT compiler generates specialized, devirtualized, and inlinable code for every input/output combination (**Monomorphization**). This approach maintains "hand-written" performance and ensures **zero-allocation** by eliminating **boxing** of value types. To avoid the resulting $M \times N$ method branching explosion in hand-written source code, a **Source Generator** (`FormatDispatcherGenerator`) is utilized to automatically generate the dispatch logic.

### Alternatives Considered

**A — Separate `CsvActionApplier` and `JsonLinesActionApplier`**
Rejected: The action → output-plan translation is identical for both formats.
Duplicating it would create a maintenance burden and risk divergence.

**B — JSON Lines input reader: `StreamReader` vs `PipeReader` vs `RowReader` + `RowIndexer`**

Three candidates were evaluated for reading JSON Lines in the batch path.

| | `StreamReader.ReadLine()` | `PipeReader.Create(stream)` | `RowReader` + `RowIndexer` |
|---|---|---|---|
| File scans | 1 | 1 | 2 (index + read) |
| Per-line `string` alloc | Yes | No | No |
| `CellExtractor` compatibility | Requires `Encoding.UTF8.GetBytes` | Requires span extraction from `ReadOnlySequence<byte>` | **Direct** (`ReadOnlyMemory<byte>`) |
| New infrastructure required | None | New sequential scan logic | None (existing code) |
| Memory profile | O(1) lines in-flight | O(1) lines in-flight | O(batch) = O(1,000) lines in-flight |
| Complexity | Low | Medium | Low |

*`StreamReader.ReadLine()`* — Pros: simplest code, single scan.
Cons: allocates one `string` per line (GC pressure for large files); requires
byte conversion before passing to `CellExtractor`.

*`PipeReader.Create(stream)`* — Pros: no per-line `string` allocation; single
scan; leverages backpressure.
Cons: requires new logic to detect line boundaries within `ReadOnlySequence<byte>`
segments; that logic partially duplicates `RowScanner`, which is already
tested and proven in `RowIndexer`.

*`RowReader` + `RowIndexer`* — Pros: reuses existing, well-tested code with no
new infrastructure; returns `ReadOnlyMemory<byte>` that `CellExtractor.ExtractCell`
accepts directly without conversion; memory-mapped I/O avoids OS buffer-copy
overhead on the read path.
Cons: requires two file scans (one for `RowIndexer.BuildIndex()`, one for
`RowReader.ReadLineBytes()`). The extra scan is acceptable because `BuildIndex()`
only counts newlines (no JSON parsing) and completes in the same order of
magnitude as the schema scan already performed by `IncrementalSchemaScanner`.

**Decision: `RowReader` + `RowIndexer`** — The direct `ReadOnlyMemory<byte>`
compatibility with `CellExtractor`, combined with the reuse of proven code and
mmap-backed I/O, outweighs the cost of a second file scan. The alternative scan
cost is low, and no new parsing logic is needed.

**C — Use `System.CommandLine` for argument parsing**
Rejected: The current flag surface is minimal (`apply`, `--input`, `--recipe`,
`--output`). A full command-line library would add a non-AOT-safe dependency
and significant complexity for four flags. This is documented as accepted debt
for a future CLI expansion.

**D — Use Interfaces (Dynamic Polymorphism) for Readers/Writers**
Rejected: Virtual method calls in the hot path (millions of rows) prevent inlining and devirtualization. Furthermore, passing `ref struct` types like `ReadOnlySpan<byte>` through interfaces is restrictive and often forces heap allocations (boxing), violating the Zero-Allocation requirement.

**E — Use Source Generators to generate $M \times N$ combinations**
**Decision: Accepted** — While Static Polymorphism delegates the actual high-performance looping to a generic `Process<T, U>` method, instantiating the correct struct factories and calling that method for every format combination results in rigid, complex, and unmaintainable `if/else` or `switch` boilerplate in the main `Runner`. By employing a Roslyn Source Generator (`FormatDispatcherGenerator`), the app discovers implementations marked with `[RecordReader]` and `[RecordWriter]` attributes and automatically emits the complete $M \times N$ dispatch matrix at compile time. This hides the branching complexity entirely and drastically simplifies the orchestration code, providing the best of both worlds: zero-allocation execution and elegant, unpolluted application logic.

### Consequences

- Adding more output formats (Parquet, TSV) requires only implementing a new `struct` reader/writer factory and attaching the appropriate attribute; the generic `Runner` or `Processor` logic remains unchanged, and the Source Generator automatically wire-ups the new dispatch paths.
- Cross-format conversion (CSV → JSON Lines and vice versa) works out of the
  box at no extra cost.
- JSON Lines batch processing performs two full file scans (index + read).
- The `apply` subcommand's flag parsing (`ArgumentParser`) is hand-rolled and
  not shared across subcommands; if the CLI surface grows significantly,
  replacing it with `System.CommandLine` is the natural next step.
- `ActionApplier.BuildOutputSchema` is pure and stateless, making it
  straightforward to unit-test in isolation from the file system.
- Performance is maximized through Monomorphization and Inlining by the compiler, despite the high-level abstraction.
