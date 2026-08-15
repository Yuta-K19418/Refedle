# Design: CLI Batch Column Resolution

## In Scope

- Remove the CLI batch pipeline's dependency on the type-inferring pre-scan (`IncrementalSchemaScanner`). The TUI side is out of scope and unchanged.
- Split column-name resolution into two phases — a phase that determines the full, ordered set of column names (no value parsing, no type inference) followed by a phase that processes rows using that fixed set — applied uniformly across all four input/output format combinations, all sharing the same processing path (`RecordProcessor`).
- Simplify the CLI-side schema representation accordingly, since it no longer needs to carry `ColumnType`.
- Update tests so column resolution is verified against the entire input, not a partial scan.

## Out of Scope

- **TUI-side column scanning and type inference** (`IncrementalSchemaScanner` itself, `ModeController.cs`, `FileDialogHandler.cs`) — unchanged, since the TUI still needs a typed schema.
- **`ComparisonType`-based filter type resolution** — unchanged; column-name resolution and per-row value-type resolution are separate layers.
- **New recipe action types** (e.g. an explicit output-column declaration) — not added; the two-phase approach makes this unnecessary.
- **`CastColumnAction` not being reflected in CLI batch output** — not fixed here; noticed during investigation but unrelated to this issue.

## Implementation Phases

### Phase 1: New column-name resolution logic

Introduces the two building blocks used to determine column names without type inference. Neither is wired into `Runner` yet — that's Phase 2.

#### CSV: header-only read

**File**: `src/Engine/IO/Csv/ColumnNameScanner.cs` (new)

```csharp
using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Reads only the header row of a CSV file to determine column names, without scanning
/// any data rows or inferring types. Used by the CLI batch pipeline, which no longer needs
/// column types (see design_cli_batch_column_resolution.md).
/// </summary>
public static class ColumnNameScanner
{
    public static IReadOnlyList<string> ScanColumnNames(string filePath)
    {
        using var reader = Sep.New(',').Reader().FromFile(filePath);
        var header = reader.Header;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new string[header.ColNames.Count];
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            var name = header.ColNames[i];
            var resolvedName = string.IsNullOrWhiteSpace(name) ? $"Column{i + 1}" : name;

            if (!seen.Add(resolvedName))
            {
                throw new InvalidOperationException($"Duplicate column name found: '{resolvedName}'");
            }

            names[i] = resolvedName;
        }

        return names;
    }
}
```

Placed alongside `Engine/IO/Csv/DataRowReader.cs`, which already depends on `nietras.SeparatedValues` — consistent with the existing dependency footprint of this namespace. Blank-name auto-naming (`ColumnN`) mirrors `App/Schema/Csv/IncrementalSchemaScanner.ReadColumnNames()` — CLI batch mode already goes through that method today via `IncrementalSchemaScanner`, so this preserves existing behavior for malformed headers rather than introducing new handling.

Duplicate-name rejection is new: `TableSchema.Columns`'s setter used to throw on duplicate names (`src/Engine/Models/TableSchema.cs`), and CLI batch mode went through `TableSchema` today via `IncrementalSchemaScanner` — so a CSV with duplicate headers currently fails. Once the CLI path no longer builds a `TableSchema` (Phase 3), that check disappears unless replaced here; without it, duplicate headers would silently resolve to whichever column happens to win the last write in `ActionApplier.BuildOutputSchema`'s name-keyed dictionary. Checked here rather than in the shared `ColumnNameResolver` dispatcher (Phase 2): JSON Lines can't produce duplicates in the first place (`PropertyNameScanner` already dedupes via its own `HashSet`), so validating at the one place duplicates can actually occur is more direct than a format-agnostic check that would only ever fire for CSV.

#### JSON Lines: property-name-only scan

**File**: `src/Engine/IO/JsonLines/PropertyNameScanner.cs` (new)

```csharp
namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Collects JSON object property names across JSON Lines rows, in first-appearance order,
/// without inferring value types. Used by the CLI batch pipeline, which no longer needs
/// column types (see design_cli_batch_column_resolution.md).
/// </summary>
public static class PropertyNameScanner
{
    /// <summary>
    /// Scans one batch of raw lines, adding any newly-seen property names to
    /// <paramref name="seen"/>/<paramref name="order"/>. Intended to be called once per batch
    /// by a caller reading a JSON Lines file in bounded-size chunks (see Phase 2), so the same
    /// pair of accumulator collections is shared and grown across repeated calls rather than
    /// each call allocating and returning its own list.
    /// </summary>
    public static void ScanPropertyNames(IReadOnlyList<JsonRawBytes> rawLines, HashSet<string> seen, List<string> order)
    {
        foreach (var line in rawLines)
        {
            ScanLine(line.Span, seen, order);
        }
    }

    private static void ScanLine(ReadOnlySpan<byte> line, HashSet<string> seen, List<string> order)
    {
        try
        {
            var reader = new Utf8JsonReader(line);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyName = reader.GetString() ?? throw new UnreachableException("GetString() returned null on a PropertyName token.");
                if (seen.Add(propertyName))
                {
                    order.Add(propertyName);
                }

                if (!reader.Read())
                {
                    return;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }
            }
        }
        catch (JsonException)
        {
            // Malformed line: skip it, matching SchemaScanner.RefineSchema's fail-soft behavior.
        }
    }
}
```

Deliberately separate from `Engine/IO/JsonLines/SchemaScanner.cs` (already 342 lines) rather than added to it — type inference and property-name walking are different concerns, and combining them would push that file further past the 300-line guideline. The per-line walking shape mirrors `SchemaScanner.ScanLine`/`ScanProperty`, minus `TypeInferrer`/`ColumnTypeResolver`.

**Why accumulator parameters instead of a return value**: `RowReader.ReadLines` copies each line into a newly-allocated `byte[]` (`src/Engine/IO/JsonLines/RowReader.cs:150-152`), so reading an entire file in one call would hold every line in memory simultaneously. The caller reads in fixed-size batches instead (the same pattern `IncrementalSchemaScannerBase.ExecuteBackgroundScan` already uses), so each batch's `List<JsonRawBytes>` becomes unreachable — and eligible for GC — once the next batch is read, keeping peak memory bounded to one batch regardless of file size. This only works if `ScanPropertyNames` accumulates into collections the caller keeps across the whole loop, rather than allocating and returning a new list per batch:

```csharp
var seen = new HashSet<string>(StringComparer.Ordinal);
var order = new List<string>();
var lineIndex = 0L;

while (lineIndex < rowIndexer.TotalRows)
{
    var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(lineIndex);
    var lines = rowReader.ReadLines(byteOffset, rowOffset, batchSize); // e.g. 1000 at a time
    if (lines.Count == 0)
    {
        break;
    }

    PropertyNameScanner.ScanPropertyNames(lines, seen, order);
    lineIndex += lines.Count;
}
```

This matches the `List<T>`/`Dictionary<K,V>` "shared accumulator across repeated calls" exception in `.claude/rules/csharp-standards.md`. The loop itself belongs to whatever drives the full-file scan (Phase 2) — shown here only to justify this method's signature.

#### Affected Files (Phase 1)

| File | Change |
|---|---|
| `src/Engine/IO/Csv/ColumnNameScanner.cs` | New; header-only column name read |
| `src/Engine/IO/JsonLines/PropertyNameScanner.cs` | New; property-name-only scan, no type inference |
| `tests/Refedle.Tests/Engine/IO/Csv/ColumnNameScannerTests.cs` | New; `ScanColumnNames` test cases |
| `tests/Refedle.Tests/Engine/IO/JsonLines/PropertyNameScannerTests.cs` | New; `ScanPropertyNames` test cases |

### Phase 2: Wire `Runner` to the new column-name resolution

Introduces the per-format dispatch and the JSON Lines full-file batch-read loop, then replaces `Runner.ScanInputSchemaAsync`'s use of `IncrementalSchemaScanner`.

#### Dispatch: plain `switch`, not a source generator

Unlike `FormatDispatcherGenerator` (reader × writer), this dispatch is not combinatorial — it depends on `inputFormat` only, so it stays linear (one `case` per format) as new formats are added, never an M×N matrix. It also isn't a hot path (called once per CLI run, not once per row), so there's no boxing/devirtualization concern motivating struct+generics either. Neither condition that justifies `FormatDispatcherGenerator` (see `design_cli_headless_batch_processing.md`'s Decision Record) applies here, so a hand-written `switch` in one small class is used instead — `Runner.cs` still never branches by hand; it always calls the same one line.

**File**: `src/App/Cli/ColumnNameResolver.cs` (new)

```csharp
namespace Refedle.App.Cli;

internal static class ColumnNameResolver
{
    private const int BatchSize = 1000;

    public static IReadOnlyList<string> ResolveColumnNames(DataFormat inputFormat, string inputFile, CancellationToken ct) =>
        inputFormat switch
        {
            DataFormat.Csv => ColumnNameScanner.ScanColumnNames(inputFile),
            DataFormat.JsonLines => ResolveJsonLinesColumnNames(inputFile, ct),
            _ => throw new NotSupportedException($"Unsupported format: {inputFormat}"),
        };

    private static IReadOnlyList<string> ResolveJsonLinesColumnNames(string inputFile, CancellationToken ct)
    {
        var rowIndexer = new RowIndexer(inputFile);
        rowIndexer.BuildIndex(ct);

        using var rowReader = new RowReader(inputFile);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        var lineIndex = 0L;

        while (lineIndex < rowIndexer.TotalRows)
        {
            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = rowIndexer.GetCheckPoint(lineIndex);
            var lines = rowReader.ReadLines(byteOffset, rowOffset, BatchSize);
            if (lines.Count == 0)
            {
                break;
            }

            PropertyNameScanner.ScanPropertyNames(lines, seen, order);
            lineIndex += lines.Count;
        }

        return order;
    }
}
```

`ResolveJsonLinesColumnNames` mirrors `IncrementalSchemaScannerBase.ExecuteBackgroundScan`'s loop shape (`RowIndexer.GetCheckPoint` + `RowReader.ReadLines` in batches of `BatchSize`), but walks the *entire* file in one synchronous pass instead of starting at row 200 and continuing in the background — this makes a column first appearing after row 200 structurally impossible to miss, rather than something handled as a special case.

Zero-record JSON Lines input (an empty file, or a file containing only blank lines) resolves to an empty column list rather than failing: `ResolveJsonLinesColumnNames` returns before constructing `RowReader` when `rowIndexer.TotalRows == 0`, since `RowReader`'s underlying `MmapService.Open` rejects zero-byte files. An empty column list flows through unchanged — `BuildOutputSchema` produces an empty output schema, and CSV output writes a header-only file while JSON Lines output writes zero bytes.

No `Task.Run`, no `async`/`ValueTask`: neither branch does genuine asynchronous I/O — `ColumnNameScanner.ScanColumnNames` uses `Sep`'s synchronous `FromFile` (not `FromFileAsync`), and `RowIndexer`/`RowReader` are mmap-backed and synchronous. Unlike `IRecordReaderFactory<TReader>.CreateAsync` (which returns `ValueTask<TReader>` because it's an interface shared with `CsvRecordReaderFactory`, whose implementation genuinely awaits `FromFileAsync`), `ColumnNameResolver` has no interface to conform to, so there's no structural reason to shape it as awaitable. A plain synchronous method is simpler and more honest about what it does.

#### `Runner.cs` changes

**File**: `src/App/Cli/Runner.cs`

```csharp
var inputFormat = DetectFileFormat(args.InputFile);
var outputFormat = DetectFileFormat(args.OutputFile);

var columnNames = ColumnNameResolver.ResolveColumnNames(inputFormat, args.InputFile, ct);

var outputSchemaResult = ActionApplier.BuildOutputSchema(columnNames, recipe.Actions);
// ... existing IsFailure check unchanged ...

return await Generated.FormatDispatcher.DispatchAsync(inputFormat, outputFormat, args, columnNames, outputSchema, logger, ct).ConfigureAwait(false);
```

`ScanInputSchemaAsync` is deleted, along with the `using Refedle.App.Schema.Csv;` import and the fully-qualified `Refedle.App.Schema.JsonLines.IncrementalSchemaScanner` reference. `ct` is now threaded into column resolution (previously `ScanInputSchemaAsync` didn't accept a token at all).

`ActionApplier.BuildOutputSchema` taking `columnNames: IReadOnlyList<string>` instead of `TableSchema` is a Phase 3 change — shown here as the end state for context. `DispatchAsync` still takes the resolved input column list as a parameter (retyped from `TableSchema inputSchema` to `IReadOnlyList<string> inputColumnNames`, not dropped): `JsonLinesRecordReader`'s constructor needs it independently of `BatchOutputSchema` to build its `ColumnIndex → property name bytes` lookup for filter evaluation (`_filterIndexToNameBytes`, see Phase 3), so it must still flow all the way through `DispatchAsync` → `Run{X}To{Y}Async` → `IRecordReaderFactory<TReader>.CreateAsync`, just no longer as a `TableSchema`.

#### Affected Files (Phase 2)

| File | Change |
|---|---|
| `src/App/Cli/ColumnNameResolver.cs` | New; per-format dispatch (`switch`) and JSON Lines full-file batch-read loop |
| `src/App/Cli/Runner.cs` | Replace `ScanInputSchemaAsync`/`IncrementalSchemaScanner` usage with `ColumnNameResolver.ResolveColumnNames` |
| `tests/Refedle.Tests/App/Cli/ColumnNameResolverTests.cs` | New; per-format dispatch, JSON Lines batch-boundary and cancellation cases |
| `tests/Refedle.Tests/App/Cli/RunnerTests.cs` | Update/add integration cases: a column first appearing after row 200 is included in CLI batch output |

### Phase 3: Simplify `ActionApplier.BuildOutputSchema` and remove `TableSchema` from the CLI path

Removes the last remnants of `TableSchema`/`ColumnType` from the CLI batch pipeline. `TableSchema` itself is untouched (TUI still needs it) — this phase only changes what the CLI path passes around.

#### `ActionApplier.BuildOutputSchema`

**File**: `src/Engine/ActionApplier.cs`

```csharp
public static Result<BatchOutputSchema> BuildOutputSchema(
    IReadOnlyList<string> columnNames,
    IReadOnlyList<MorphAction> actions
)
{
    ArgumentNullException.ThrowIfNull(columnNames);
    ArgumentNullException.ThrowIfNull(actions);

    var workingColumns = columnNames
        .Select((name, index) => (Name: name, ColumnIndex: index, OutputName: name))
        .ToList();
    var nameToWorkingIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    for (var i = 0; i < workingColumns.Count; i++)
    {
        nameToWorkingIndex[workingColumns[i].Name] = i;
    }

    List<BatchFilterSpec> filterSpecs = [];
    Dictionary<int, CellTransformSpec> transformsByWorkingIndex = [];

    foreach (var action in actions)
    {
        var result = ApplyAction(action, workingColumns, nameToWorkingIndex, filterSpecs, transformsByWorkingIndex);
        if (result.IsFailure)
        {
            return Results.Failure<BatchOutputSchema>(result.Error);
        }
    }

    var outputColumns = BuildOutputColumns(workingColumns, nameToWorkingIndex, transformsByWorkingIndex);
    return Results.Success(new BatchOutputSchema(outputColumns, filterSpecs));
}

private static Result ApplyAction(
    MorphAction action,
    List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex,
    List<BatchFilterSpec> filterSpecs,
    Dictionary<int, CellTransformSpec> transformsByWorkingIndex
) =>
    action switch
    {
        RenameColumnAction rename => ApplyRename(rename, workingColumns, nameToWorkingIndex),
        DeleteColumnAction delete => ApplyDelete(delete, nameToWorkingIndex),
        CastColumnAction => Results.Success(), // no-op: no ColumnType tracked anymore (see Out of Scope)
        FilterAction filter => ApplyFilter(filter, workingColumns, nameToWorkingIndex, filterSpecs),
        FillColumnAction fill => ApplyFill(fill, nameToWorkingIndex, transformsByWorkingIndex),
        FormatTimestampAction formatTimestamp
            => ApplyFormatTimestamp(formatTimestamp, nameToWorkingIndex, transformsByWorkingIndex),
        _ => throw new UnreachableException($"Unhandled action type: {action.GetType().Name}"),
    };

private static Result ApplyRename(
    RenameColumnAction rename,
    List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex
)
{
    if (!nameToWorkingIndex.TryGetValue(rename.OldName, out var idx))
    {
        return Results.Success();
    }

    var (name, columnIndex, _) = workingColumns[idx];
    workingColumns[idx] = (name, columnIndex, rename.NewName);
    nameToWorkingIndex.Remove(rename.OldName);
    nameToWorkingIndex[rename.NewName] = idx;
    return Results.Success();
}

// ApplyDelete: unchanged, never referenced workingColumns' Type either.

private static Result ApplyFilter(
    FilterAction filter,
    List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex,
    List<BatchFilterSpec> filterSpecs
)
{
    if (!nameToWorkingIndex.TryGetValue(filter.ColumnName, out var idx))
    {
        return Results.Success();
    }

    var (_, columnIndex, _) = workingColumns[idx];
    filterSpecs.Add(
        new BatchFilterSpec(
            SourceColumnIndex: columnIndex,
            ComparisonType: filter.ComparisonType,
            Operator: filter.Operator,
            Value: filter.Value
        )
    );
    return Results.Success();
}

// ApplyFill / ApplyFormatTimestamp: unchanged, never touched workingColumns' Type in the first place.

// Filters out deleted columns and preserves working-column order.
private static List<BatchOutputColumn> BuildOutputColumns(
    List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex,
    Dictionary<int, CellTransformSpec> transformsByWorkingIndex
)
{
    List<BatchOutputColumn> outputColumns = [];
    foreach (var kvp in nameToWorkingIndex.OrderBy(kvp => kvp.Value))
    {
        var (name, _, outputName) = workingColumns[kvp.Value];
        var transform = transformsByWorkingIndex.GetValueOrDefault(kvp.Value);
        outputColumns.Add(new BatchOutputColumn(SourceName: name, OutputName: outputName, Transform: transform));
    }

    return outputColumns;
}
```

The `(string Name, ColumnType Type, int ColumnIndex, string OutputName)` tuple loses its `Type` element everywhere. `ApplyCast` is reduced to an inline `Results.Success()` in the switch — it never had anything to write to once `Type` is gone, and nothing downstream ever read it anyway (see Out of Scope: `CastColumnAction` not being reflected in CLI batch output — this makes that pre-existing gap more visible, not different). `using Refedle.Engine.Types;` is removed from this file — `ColumnType` was its only use, and nothing else in the file references that namespace.

#### `JsonLinesRecordReader` constructor

**File**: `src/App/Cli/JsonLinesRecordReader.cs`

```csharp
public JsonLinesRecordReader(RowIndexer rowIndexer, RowReader rowReader, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema)
{
    _rowIndexer = rowIndexer;
    _rowReader = rowReader;

    _columnNameUtf8Bytes = [.. outputSchema.Columns
        .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

    _filterIndexToNameBytes = new Dictionary<int, ReadOnlyMemory<byte>>(inputColumnNames.Count);
    for (var i = 0; i < inputColumnNames.Count; i++)
    {
        _filterIndexToNameBytes[i] = Encoding.UTF8.GetBytes(inputColumnNames[i]).AsMemory();
    }

    _filters = outputSchema.Filters;
    // ... remaining field initializers unchanged ...
}
```

Replaces `inputSchema.Columns.ToDictionary(c => c.ColumnIndex, c => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(c.Name))` — same result (index → UTF-8 name bytes), built from a plain list instead of `TableSchema.Columns`.

#### Reader factory interface and implementations

**File**: `src/App/Cli/Factories/IRecordReaderFactory.cs`

```csharp
internal interface IRecordReaderFactory<TReader> where TReader : struct, IRecordReader
{
    ValueTask<TReader> CreateAsync(Arguments args, IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
```

`CsvRecordReaderFactory.CreateAsync` and `JsonLinesRecordReaderFactory.CreateAsync` retype their `inputSchema: TableSchema` parameter to `inputColumnNames: IReadOnlyList<string>` to match. `CsvRecordReaderFactory` still ignores it entirely (unchanged from today); `JsonLinesRecordReaderFactory` passes it straight through to the `JsonLinesRecordReader` constructor shown above.

#### `FormatDispatcherGenerator.cs` (hand-written generator source)

**File**: `src/Generators/FormatDispatcherGenerator.cs`

Every emitted `TableSchema inputSchema` parameter (in both `DispatchAsync` and each generated `Run{Reader}To{Writer}Async`) becomes `IReadOnlyList<string> inputColumnNames`, and each call site forwards `inputColumnNames` instead of `inputSchema`. The emitted `using Refedle.Engine.Models;` line is removed — `TableSchema` was its only reason to be there; `BatchOutputSchema`/`CellTransformSpec` live in `Refedle.Engine`/`Refedle.Engine.Models`... (verify at implementation time whether any other emitted type still needs that namespace before removing the `using`).

#### `Runner.cs` using cleanup

`using Refedle.Engine.Models;` is removed if `TableSchema` (or any other `Refedle.Engine.Models` type) is no longer referenced anywhere in the file once `ScanInputSchemaAsync` is gone (verify at implementation time).

#### Affected Files (Phase 3)

| File | Change |
|---|---|
| `src/Engine/ActionApplier.cs` | `BuildOutputSchema` takes `IReadOnlyList<string>`; `workingColumns` tuple drops `ColumnType`; `ApplyCast` becomes an inline no-op; unused `using Refedle.Engine.Types;` removed |
| `src/App/Cli/JsonLinesRecordReader.cs` | Constructor takes `IReadOnlyList<string> inputColumnNames` instead of `TableSchema inputSchema` |
| `src/App/Cli/Factories/IRecordReaderFactory.cs` | `CreateAsync` parameter retyped |
| `src/App/Cli/Factories/CsvRecordReaderFactory.cs` | Parameter retyped (still unused) |
| `src/App/Cli/Factories/JsonLinesRecordReaderFactory.cs` | Parameter retyped, passed through |
| `src/Generators/FormatDispatcherGenerator.cs` | Emitted signatures retyped; possibly drops an emitted `using` |
| `src/App/Cli/Runner.cs` | Possible unused-`using` cleanup |
| `tests/Refedle.Tests/Engine/ActionApplierTests.cs` | ~30 call sites: replace constructed `TableSchema` with plain `IReadOnlyList<string>` (e.g. `["A", "B"]`); test intent unchanged |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.cs` | Shared `BuildSchemas` helper (constructs `TableSchema` today) updated to build `IReadOnlyList<string>` instead |

## Architecture Decision Log

### ADR-1: Why column resolution is unified across all four input/output format combinations

**Context**

For JSON Lines → CSV, the full set of column names must be known before writing begins, because CSV can only write its header once. This constraint is unavoidable and is the starting point for this design.

Given that this "resolve columns upfront" mechanism has to exist at all for JSON Lines → CSV, `ActionApplier.BuildOutputSchema` (which applies the recipe's actions to determine the output columns) is simplest to implement if it can always assume the full input column list is already known. If JSON Lines → JSON Lines were the one exception (each output row deriving its columns from that row's own properties), `BuildOutputSchema` would need a second shape for that one case — one that doesn't receive a column list at all, but instead applies actions dynamically per row.

The question this ADR resolves: should the "resolve columns upfront" mechanism that JSON Lines → CSV cannot avoid be treated as a universal precondition `BuildOutputSchema` can always rely on, applied uniformly to every combination (including JSON Lines → JSON Lines, which doesn't strictly need it)?

**Options**

- **A — Per-row dynamic column determination for JSON Lines → JSON Lines only**: each output row derives its columns from that row's own actual properties.
  - Rejected: for this one combination, both `BuildOutputSchema` and `RecordProcessor.ProcessAsync` would need a second shape that doesn't assume a fixed, pre-known column list — a structural fork introduced for a single combination.
- **B — Branch per combination on whether upfront resolution is needed**: two passes only for JSON Lines → CSV; CSV input stays trivial (header only); JSON Lines → JSON Lines stays on Option A.
  - Rejected: carries the same structural problem as A (JSON Lines → JSON Lines still needs a second shape for `BuildOutputSchema`/`RecordProcessor.ProcessAsync`), and adds a further branch — "does this combination need upfront resolution?" — that only increases complexity and maintenance cost on top of it.
- **C (Adopted) — Always resolve columns upfront, regardless of combination**: CSV input just reads its header. JSON Lines input walks every line once, collecting the union of property names (no type inference). `BuildOutputSchema`, `RecordProcessor.ProcessAsync`, the source generator, and the reader/writer contracts all keep a single shape across all four combinations.

**Rationale**

- `BuildOutputSchema` can be implemented against a single precondition — the input column list is always known upfront — with no second shape needed for JSON Lines → JSON Lines.
- No combination-specific special-casing is needed anywhere in the CLI pipeline; `RecordProcessor`, the generator, and the reader/writer contracts stay untouched.
- The extra full-file walk this adds for JSON Lines → JSON Lines is a real runtime cost that cannot honestly be called small — it hasn't been measured. The trade-off made here is prioritizing the low maintenance cost and future changeability of a single processing path over avoiding that cost.

**Consequence**

- JSON Lines input always pays for one full-file walk to resolve column names before processing begins, regardless of the output format.
- Every combination pays this cost uniformly, including JSON Lines → JSON Lines, where it was structurally avoidable. A concrete runtime overhead is accepted in exchange for zero special-casing across the pipeline, `BuildOutputSchema` included.
- If JSON Lines → JSON Lines performance on very large files becomes a real bottleneck later, revisiting this decision (reintroducing per-combination branching or a dedicated processing path) remains possible as a follow-up.

## Notable Test Cases

- **`ColumnNameScanner.ScanColumnNames`**: normal header → names in order; blank/whitespace-only header cell → auto-named `ColumnN`; duplicate header names → throws `InvalidOperationException`; a blank cell whose auto-generated name (`ColumnN`) collides with an actual header name elsewhere → throws (auto-naming and explicit names share the same uniqueness check).
- **`PropertyNameScanner.ScanPropertyNames`**: empty input → accumulator unchanged; single line → that line's keys appended in order; multiple calls with overlapping keys across batches (simulating repeated calls sharing one accumulator) → union, first-appearance order, no duplicates; a key appearing only in a later batch is included; malformed JSON line → skipped, does not affect other lines in the same or later batches; non-object line (e.g. a JSON array or scalar) → skipped like a malformed line.
- **`ColumnNameResolver.ResolveColumnNames`**: CSV → delegates to `ColumnNameScanner`; JSON Lines with more rows than one batch → column names from every batch are included (not just the first `BatchSize` rows); JSON Lines with a column first appearing beyond the old 200-row initial-scan cap → included; cancelled token → observably stops (via `ct.ThrowIfCancellationRequested()` in the loop) rather than completing the full file; unsupported `DataFormat` → throws `NotSupportedException`.
- **`ActionApplier.BuildOutputSchema`** (existing test file, signature updated): all existing cases continue to pass with `IReadOnlyList<string>` in place of `TableSchema`; `CastColumnAction` cases specifically assert it remains a no-op (output columns/filters unaffected) — same observable behavior as before, now via the inline `Results.Success()` case instead of a dedicated method.
- **`JsonLinesRecordReader`** (existing test file, constructor updated): filter evaluation against a specific `SourceColumnIndex` still resolves to the correct property name via the new `inputColumnNames`-based lookup — same cases as today, just re-pointed at the new constructor shape.
