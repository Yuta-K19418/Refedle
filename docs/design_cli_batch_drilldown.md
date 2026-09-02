## In Scope

- Replay DrillDown-scoped recipes in CLI batch processing for JSON Array / JSON Object / JSON Lines input
- Add JSON as a CLI output format
- Error handling for invalid input (JSON Array/Object input with no recipe, or with a recipe that has no DrillDown scope)
- Add tests

## Out of Scope

- Non-DrillDown (bare) CLI mode for JSON Array/Object
- Changes to the TUI

## Specifications

### Input Format Detection

CLI input format detection reuses the same logic as `FormatDetector` (TUI): for a `.json` input file, the first non-whitespace byte (BOM-aware) distinguishes `JsonObject` (`{`) from `JsonArray` (`[`).

### DrillDown Kind Resolution

The DrillDown kind is determined the same way as in the TUI: it is inferred from the detected input `DataFormat`, not stored in the recipe. `JsonObject` resolves as Single DrillDown; `JsonLines`/`JsonArray` resolve as Full Aggregation DrillDown.

### Input Format

#### CSV

No change. The existing `CsvRecordReaderFactory` is used as-is; DrillDown does not apply to CSV input.

#### JSON Lines

If `recipe.DrillDownKeyPath` is null, the existing `JsonLinesRecordReaderFactory` is used as-is (no change). Otherwise, the new Full Aggregation DrillDown path resolves the input via `FullAggregationScanner`.

#### JSON Array

New support. Requires `recipe.DrillDownKeyPath` (error if null). Resolved via `FullAggregationScanner` (Full Aggregation DrillDown).

#### JSON Object

New support. Requires `recipe.DrillDownKeyPath` (error if null). Resolved via `KeyPathNodeResolver` + `DrillDownSchemaExtractor` (Single DrillDown).

### Output Format

#### CSV

No change. When `--output` is `.csv`, the existing `CsvRecordWriter` is used as-is, regardless of DrillDown kind.

#### JSON Lines

No change. When `--output` is `.jsonl`, the existing `JsonLinesRecordWriter` is used as-is, regardless of DrillDown kind.

#### JSON

When `--output` is `.json`, a new `JsonArrayRecordWriter` is added, writing a JSON array (`[`, then each record as `{...}`, comma-separated, then `]`). JSON Array is used unconditionally — regardless of DrillDown kind or the actual record count — because a JSON Object cannot naturally represent more than one row.

## Implementation Phases

### Phase 1: Source Generator Test Infrastructure

Set up exact-match test coverage for `FormatDispatcherGenerator` before modifying its output in the next phase. Use the `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing` harness with `DefaultVerifier` (the `.XUnit` variant is deprecated — its `XUnitVerifier` carries `[Obsolete]`, which the zero-warnings policy rejects) to diff the full generated source against an expected string per test case, covering the current dispatch behavior (CSV/JSON Lines reader/writer pairs) as a regression baseline before Phase 2 changes the generated `CreateAsync` call sites.

```
tests/Refedle.Tests.csproj
  + PackageReference: Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing
  + PackageReference: Microsoft.CodeAnalysis.CSharp.Workspaces (pinned to align the harness's Roslyn with Refedle.Generators')
  + ProjectReference: src/Generators/Refedle.Generators.csproj
```

**Affected Files**

| File | Change |
|---|---|
| `tests/Refedle.Tests/Generators/FormatDispatcherGeneratorTests.cs` | New — exact-match generated-source snapshots for the current CSV/JSON Lines reader/writer dispatch (updated in Phase 2 when the `CreateAsync` signature changes) |

### Phase 2: Decompose Arguments in Reader/Writer Factory Signatures

Replace the shared `Arguments args` parameter in `IRecordReaderFactory<TReader>.CreateAsync`/`IRecordWriterFactory<TWriter>.CreateAsync` with only the individual values each side needs, and add a `drillDownKeyPath` parameter to the reader side (unused until a later phase). Add a `drillDownKeyPath` parameter to `ColumnNameResolver.ResolveColumnNames` as well (also unused until a later phase). Pure refactor — no behavioral change. **Note:** the unused `drillDownKeyPath` parameter may trigger an unused-parameter warning under this project's zero-warnings policy; this is accepted for this phase and resolved once Phase 3 wires up actual usage.

#### Reader/Writer Factory Signatures

```csharp
// IRecordReaderFactory<TReader>
ValueTask<TReader> CreateAsync(
    string inputFile,
    IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
    IReadOnlyList<string> inputColumnNames,
    BatchOutputSchema outputSchema,
    IAppLogger logger,
    CancellationToken ct);

// IRecordWriterFactory<TWriter>
ValueTask<TWriter> CreateAsync(
    string outputFile,
    BatchOutputSchema outputSchema,
    IAppLogger logger,
    CancellationToken ct);
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/Factories/IRecordReaderFactory.cs` | `CreateAsync` signature change |
| `src/App/Cli/Factories/IRecordWriterFactory.cs` | `CreateAsync` signature change |
| `src/App/Cli/Factories/CsvRecordReaderFactory.cs` | Follow new signature (`args.InputFile` → `inputFile`) |
| `src/App/Cli/Factories/CsvRecordWriterFactory.cs` | Follow new signature (`args.OutputFile` → `outputFile`) |
| `src/App/Cli/Factories/JsonLinesRecordReaderFactory.cs` | Follow new signature |
| `src/App/Cli/Factories/JsonLinesRecordWriterFactory.cs` | Follow new signature |
| `src/Generators/FormatDispatcherGenerator.cs` | Update the `CreateAsync(...)` call emitted inside the generated `Run{Reader}To{Writer}Async` methods to match the new signature |
| `src/App/Cli/Runner.cs` | Change the call into `FormatDispatcher.DispatchAsync` to pass `args.InputFile`/`args.OutputFile` instead of `args` as a whole |
| `src/App/GlobalSuppressions.cs` | Update `Target` strings that embed the full `CreateAsync` overload signatures |
| `tests/Refedle.Tests/App/Cli/CsvRecordReaderTests.cs`, `CsvRecordWriterTests.cs`, `JsonLinesRecordReaderTests.cs`, `JsonLinesRecordWriterTests.cs` | Follow new signature |
| `tests/Refedle.Tests/Generators/FormatDispatcherGeneratorTests.cs` (added in Phase 1) | Update expected generated source strings to match the new signature |
| `benchmarks/Refedle.Benchmarks/App/Cli/JsonLinesRecordReaderBenchmarks.cs` | Follow new signature |

#### ColumnNameResolver

```csharp
// ColumnNameResolver
public static IReadOnlyList<string> ResolveColumnNames(
    DataFormat inputFormat, string inputFile, IReadOnlyList<KeyPathSegment>? drillDownKeyPath, CancellationToken ct);
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/ColumnNameResolver.cs` | Signature change (add `drillDownKeyPath` parameter; existing branches ignore it) |
| `src/App/Cli/Runner.cs` | Pass `drillDownKeyPath` (always `null` at this phase) to `ColumnNameResolver.ResolveColumnNames` |

#### FormatDetector Split

```csharp
// FormatDetector
public static Result<DataFormat> DetectInputFile(string filePath);  // renamed from Detect, unchanged behavior
public static Result<DataFormat> DetectOutputFile(string filePath); // new, extension-only: .csv→Csv, .jsonl→JsonLines, .json→JsonArray, else→failure
```

Replace `Runner.cs`'s private extension-only `DetectFileFormat` with calls to `FormatDetector` — `DetectInputFile` for the input file, `DetectOutputFile` for the output file (which doesn't exist yet, so it can't be content-sniffed). As a side effect, `.json` input now detects successfully as `JsonObject`/`JsonArray` even before Phase 3 adds downstream support (it will still fail later, in `ColumnNameResolver`, with a generic unsupported-format message) — acceptable, since this branch won't be merged until the full feature is complete.

Another effect, unforeseen at design time: a zero-byte input file now fails with exit code 1 (`File is empty`) instead of being processed as zero-record input, aligning CLI behavior with the TUI's existing `FormatDetector` check. This is a deliberate, accepted narrowing — a zero-byte input file has no legitimate batch use. See the `JsonLinesRecordReader Zero-Record Path` section below for the follow-on cleanup this enables.

**Affected Files**

| File | Change |
|---|---|
| `src/App/FormatDetector.cs` | Rename `Detect` → `DetectInputFile`; add new `DetectOutputFile` (extension-only) |
| `src/App/Cli/Runner.cs` | Delete private `DetectFileFormat`; call `FormatDetector.DetectInputFile(args.InputFile)` / `FormatDetector.DetectOutputFile(args.OutputFile)`, handling `Result<DataFormat>` failure explicitly |
| `src/App/AppKeyHandler.cs`, `FileDialogHandler.cs`, `RecipeCommandHandler.cs`, `ViewManager.cs` | Rename `FormatDetector.Detect(...)` call sites to `FormatDetector.DetectInputFile(...)` |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/FormatDetectorTests.cs` | Already exists. Rename existing `Detect` test cases to `DetectInputFile`; add new test cases for `DetectOutputFile` (extension-only mapping, including `.json`→`JsonArray` and the unsupported-extension failure case) |
| `tests/Refedle.Tests/App/Cli/RunnerTests.cs` | Already exists. `RunAsync_WithUnsupportedInputExtension_ReturnsExitCode1` / `RunAsync_WithUnsupportedOutputExtension_ReturnsExitCode1`: switch the test fixture from `.json` to a genuinely never-supported extension (e.g. `.xml`) so these tests stay valid once Phase 3 adds `.json` support, and update the expected message to match `FormatDetector`'s wording. `RunAsync_WithUnknownExtension_ReturnsExitCode1`: update the expected message to match `FormatDetector`'s wording (exact message text decided at implementation time). Remove `RunAsync_JsonLinesToCsv_WithZeroRecordInput` / `RunAsync_JsonLinesToJsonLines_WithZeroRecordInput` (their `""` case is now an error, and the newline-only case is not a real scenario worth a contract); add one test asserting a zero-byte input file returns exit code 1 with the `File is empty` message |

#### JsonLinesRecordReader Zero-Record Path

`RowIndexer.TotalRows == 0` occurs only for a zero-byte file — any file with content yields at least one row. Now that `Runner` rejects zero-byte input before dispatch, neither `JsonLinesRecordReaderFactory` nor `ColumnNameResolver` can reach its `TotalRows == 0` branch, so those two branches become dead and are removed. The reader's constructor parameter drops its `?` (the factory always passes a real `RowReader`). The `_rowReader` field itself stays nullable and is still nulled in `Dispose` — matching the post-dispose fail-fast idiom used by the sibling `CsvRecordReader` / `CsvRecordWriter` / `JsonLinesRecordWriter` in the same directory; revisiting that idiom across all four is out of scope here.

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/Factories/JsonLinesRecordReaderFactory.cs` | Remove the `TotalRows == 0` branch that constructs the reader with a `null` `RowReader` |
| `src/App/Cli/JsonLinesRecordReader.cs` | Constructor parameter `RowReader? rowReader` → `RowReader rowReader`; `_rowReader` field stays `RowReader?` with the `Dispose` `_rowReader = null` and the `_rowReader is null` guard in `MoveNextAsync` kept; update the zero-record explanatory comment to reflect that `null` now only arises post-dispose |
| `src/App/Cli/ColumnNameResolver.cs` | Remove the `if (rowIndexer.TotalRows == 0) return [];` guard in `ResolveJsonLinesColumnNames` and its zero-record comment |

### Phase 3: Extract JSON Array/Object DrillDown Rows

Extract DrillDown-scoped rows from JSON Array/Object input via `DrillDownKeyPath` and expose them through a new `IRecordReader` implementation, unmodified (no recipe Actions applied). Verified against the existing CSV and JSON Lines writers.

#### DrillDownRecipeValidator

Validates that a DrillDown-scoped `Recipe` is applicable given the detected input `DataFormat`, before any format-specific processing begins. Fails when `inputFormat` is `JsonObject`/`JsonArray` and `recipe.DrillDownKeyPath` is `null`. Called from `Runner.cs` right after the recipe is loaded and the input format is detected, using the same early-return failure pattern as other `Result`-based steps. Standalone and reusable (`DataFormat`/`Recipe` in, `Result` out) — a future `--dry-run` command could call it directly alongside `RecipeManager.LoadAsync`/`FormatDetector.DetectInputFile`, without going through `Runner.RunAsync`.

```csharp
internal static class DrillDownRecipeValidator
{
    public static Result Validate(DataFormat inputFormat, Recipe recipe);
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/DrillDownRecipeValidator.cs` | New |
| `src/App/Cli/Runner.cs` | Call `DrillDownRecipeValidator.Validate(inputFormat, recipe)` after recipe load and input format detection; handle failure with the existing early-return pattern |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/DrillDownRecipeValidatorTests.cs` | New |

#### JsonObjectCellReader (shared typed cell extraction)

Both DrillDown readers below (and the existing `JsonLinesRecordReader`) decode a typed `CellData` from one JSON object's bytes by column name into a pooled `char[]`. That logic — currently inline in `JsonLinesRecordReader.GetCellData` — moves to a shared static `JsonObjectCellReader.ReadCell`, with `PooledValueBuffer` (promoted from a private nested type of `JsonLinesRecordReader` to a standalone `internal sealed class`) passed in and still owned by each reader. See ADR-7.

`src/App/Cli/JsonObjectCellReader.cs`

```csharp
internal static class JsonObjectCellReader
{
    // Body moved verbatim from JsonLinesRecordReader.GetCellData + ReadPropertyValue /
    // NumberToCellData / ObjectOrArrayToCellData / StringToCellData. The returned
    // CellData.Value span is valid until the next ReadCell call on the same buffer.
    public static CellData ReadCell(
        JsonRawBytes objectBytes, ReadOnlySpan<byte> columnNameUtf8, PooledValueBuffer valueBuffer);
}
```

Each reader keeps `private readonly Memory<byte>[] _columnNameUtf8Bytes;` and `private readonly PooledValueBuffer _valueBuffer;` (as `JsonLinesRecordReader` does today), disposes the buffer in its own `Dispose`, and implements `GetCellData(i)` as `JsonObjectCellReader.ReadCell(currentRowBytes, _columnNameUtf8Bytes[i].Span, _valueBuffer)`. `EvaluateFilters` is unchanged — it already uses the string-returning `JsonObjectCellExtractor.ExtractCell`.

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/JsonObjectCellReader.cs` | New |
| `src/App/Cli/PooledValueBuffer.cs` | New (promoted from `JsonLinesRecordReader.PooledValueBuffer.cs`, unchanged) |
| `src/App/Cli/JsonLinesRecordReader.PooledValueBuffer.cs` | Deleted (promoted) |
| `src/App/Cli/JsonLinesRecordReader.cs` | `GetCellData` delegates to `JsonObjectCellReader.ReadCell`; remove `ReadPropertyValue` / `NumberToCellData` / `ObjectOrArrayToCellData` / `StringToCellData` |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/JsonObjectCellReaderTests.cs` | New — the cell-decoding branch cases move here from `JsonLinesRecordReaderTests.GetCellData.PooledBuffer.cs` (pooled-buffer reuse, span invalidation, number/string/object/array/bool/null) |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.GetCellData.PooledBuffer.cs` | Deleted (cases moved) |

#### JsonObjectRecordReader (Single DrillDown)

New `IRecordReader` implementation for JSON Object input, reusing `KeyPathNodeResolver.ResolveSingleNode` + `DrillDownSchemaExtractor.ExtractFromNode` unchanged — no new streaming infrastructure needed, since a Single DrillDown node is bounded to one resolved reference, not the whole file. By the time this factory runs, `ColumnNameResolver` has already resolved the same input for the column set, so a null/empty `drillDownKeyPath` or a resolution failure here means a broken upstream invariant — the factory throws `InvalidOperationException` (caught by `Runner`'s outer `catch (Exception)`), not `UnreachableException`.

```csharp
[RecordReader(DataFormat.JsonObject)]
internal readonly struct JsonObjectRecordReaderFactory : IRecordReaderFactory<JsonObjectRecordReader>
{
    public ValueTask<JsonObjectRecordReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        if (drillDownKeyPath is not { Count: > 0 })
        {
            throw new InvalidOperationException("ColumnNameResolver rejects null/empty drillDownKeyPath upstream.");
        }
        // resolve node via KeyPathNodeResolver.ResolveSingleNode, extract rows via
        // DrillDownSchemaExtractor.ExtractFromNode; a Result.Failure from either → InvalidOperationException
    }
}

internal struct JsonObjectRecordReader : IRecordReader
{
    // Not readonly: MoveNextAsync advances the row cursor (same as the other IRecordReader structs).
    // Wraps the resolved child rows (IReadOnlyList<JsonRawBytes>) from DrillDownSchemaExtractor.ExtractFromNode.
    // MoveNextAsync just indexes through them — already fully resolved in memory, no further I/O.
    // Holds _columnNameUtf8Bytes + _valueBuffer; GetCellData delegates to JsonObjectCellReader.ReadCell.
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/JsonObjectRecordReader.cs` | New |
| `src/App/Cli/Factories/JsonObjectRecordReaderFactory.cs` | New |
| `src/App/Cli/ColumnNameResolver.cs` | `ResolveColumnNames` → `ResolveColumnNamesAsync` returning `Task<Result<IReadOnlyList<string>>>` (async forced by the `JsonObject` branch's `File.ReadAllBytesAsync` — MA0045); existing branches wrap in `Results.Success`. Add the `JsonObject` branch: reject null/empty `drillDownKeyPath` with the TUI message, else `KeyPathNodeResolver` + `DrillDownSchemaExtractor`, propagating failures as `Results.Failure` |
| `src/App/Cli/Runner.cs` | `await` + handle the new `Result` with the existing early-return (`Error resolving columns: {error}`); still passes `drillDownKeyPath: null` (Phase 5 wires the real value) |
| `src/App/GlobalSuppressions.cs` | Drop the `IDE0060` suppression for `ResolveColumnNames`; add a CA1001 suppression for the `JsonObjectRecordReader` struct (same false positive as `JsonLinesRecordReader`) |

#### FullAggregationRecordReader (Full Aggregation DrillDown)

New `IRecordReader` implementation for JSON Array input, streaming the file in bounded batches instead of materializing all matching rows at once (unlike `FullAggregationScanner`, which the TUI uses and which retains every row's bytes for the whole file). Built on the existing `JsonArray.RowIndexer`/`ElementReader` streaming primitives (already production-proven — same pattern `JsonLinesRecordReader` already uses for JSON Lines), with two new public Engine-layer entry points wrapping the internal `KeyPathTraverser`/`SchemaScanner` DrillDown primitives (`KeyPathTraverser` et al. are `internal` to `Refedle.Engine`, not visible to `Refedle.App`).

A generic struct (`FullAggregationRecordReader<TBatchSourceReader>`, constrained to `struct, IBatchSourceReader`) abstracts over the underlying batch source so the same reader logic can later be reused for JSON Lines DrillDown (Phase 6) without struct inheritance (not supported in C#) — each format gets its own thin struct wrapper around its existing `class`-based reader (`ElementReader`/`RowReader`), avoiding boxing via generic specialization.

`src/Engine/IO/DrillDown/FullAggregationSchemaScanner.cs`

```csharp
public static class FullAggregationSchemaScanner
{
    // Streams the whole file in batches via RowIndexer + RowReader/ElementReader,
    // applying KeyPathTraverser.ExtractRows per record and folding into schema
    // accumulators only — extracted rows are discarded per record, never retained.
    public static Result<TableSchema> Scan(
        string filePath, DataFormat format, IReadOnlyList<KeyPathSegment> keyPath, CancellationToken ct);
}
```

`src/Engine/IO/DrillDown/FullAggregationRowExtractor.cs`

```csharp
public static class FullAggregationRowExtractor
{
    // Applies KeyPathTraverser.ExtractRows to each record in an already-fetched batch.
    public static IReadOnlyList<FocusedTableRow> ExtractRows(
        IReadOnlyList<JsonRawBytes> recordBatch, IReadOnlyList<KeyPathSegment> keyPath);
}
```

`src/App/Cli/IBatchSourceReader.cs`

```csharp
internal interface IBatchSourceReader : IDisposable
{
    IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch);
}
```

`src/App/Cli/JsonArrayBatchSourceReader.cs`

```csharp
internal readonly struct JsonArrayBatchSourceReader(ElementReader reader) : IBatchSourceReader
{
    public IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch) =>
        reader.ReadElements(byteOffset, skip, fetch);
    public void Dispose() => reader.Dispose();
}
```

`src/App/Cli/FullAggregationRecordReader.cs`

```csharp
internal struct FullAggregationRecordReader<TBatchSourceReader> : IRecordReader
    where TBatchSourceReader : struct, IBatchSourceReader
{
    // Not readonly: MoveNextAsync mutates the batch cursor. Holds the source reader + KeyPath;
    // MoveNextAsync fetches a batch via ReadBatch,
    // runs FullAggregationRowExtractor.ExtractRows, and yields rows one at a time,
    // refetching the next batch once exhausted.
    // Also holds _columnNameUtf8Bytes + _valueBuffer; GetCellData delegates to JsonObjectCellReader.ReadCell.
}
```

`src/App/Cli/Factories/JsonArrayRecordReaderFactory.cs`

```csharp
[RecordReader(DataFormat.JsonArray)]
internal readonly struct JsonArrayRecordReaderFactory
    : IRecordReaderFactory<FullAggregationRecordReader<JsonArrayBatchSourceReader>>
{
    // Builds JsonArray.RowIndexer + ElementReader, wraps in JsonArrayBatchSourceReader,
    // constructs FullAggregationRecordReader.
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/Engine/IO/DrillDown/FullAggregationSchemaScanner.cs` | New |
| `src/Engine/IO/DrillDown/FullAggregationRowExtractor.cs` | New |
| `src/App/Cli/IBatchSourceReader.cs` | New |
| `src/App/Cli/JsonArrayBatchSourceReader.cs` | New |
| `src/App/Cli/FullAggregationRecordReader.cs` | New |
| `src/App/Cli/Factories/JsonArrayRecordReaderFactory.cs` | New |
| `src/App/Cli/ColumnNameResolver.cs` | Add `JsonArray` branch: call `FullAggregationSchemaScanner.Scan` |
| `src/Generators/FormatDispatcherGenerator.cs` | `ExtractCreatedType` slices at the outermost `>` (`LastIndexOf`) so a nested-generic reader type (`FullAggregationRecordReader<JsonArrayBatchSourceReader>`) survives intact |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/Engine/IO/DrillDown/FullAggregationSchemaScannerTests.cs` | New |
| `tests/Refedle.Tests/Engine/IO/DrillDown/FullAggregationRowExtractorTests.cs` | New |
| `tests/Refedle.Tests/App/Cli/FullAggregationRecordReaderTests.JsonArrayBatchSourceReader.cs` | New |
| `tests/Refedle.Tests/App/Cli/JsonArrayBatchSourceReaderTests.cs` | New — direct adapter coverage (ReadBatch, delegated disposal) |
| `tests/Refedle.Tests/App/Cli/ColumnNameResolverTests.cs` | Already exists. Add test case for the new `JsonArray` branch |
| `tests/Refedle.Tests/App/Cli/Factories/JsonArrayRecordReaderFactoryTests.cs` | New |
| `tests/Refedle.Tests/Generators/FormatDispatcherGeneratorTests.cs` | Add a nested-generic reader-factory regression case (closed generic type kept intact in the generated `ProcessAsync<...>`) |

### Phase 4: JSON Output Writer

Add the new JSON output writer (`JsonArrayRecordWriter`), which per ADR-1 always emits a JSON Array regardless of row count. Verified against every reader that exists at this point — CSV, JSON Lines, and the Phase 3 DrillDown reader.

#### `WriteFooterAsync` on `IRecordWriter`

`IRecordWriter` gains a `WriteFooterAsync` hook, called once after the record loop ends — the counterpart to `WriteHeaderAsync` at the other end of the stream. `RecordProcessor.ProcessAsync` calls it after the loop, before the existing `FlushAsync` call. `CsvRecordWriter`/`JsonLinesRecordWriter` implement it as a no-op, the same way `JsonLinesRecordWriter.WriteHeaderAsync` is already a no-op today (only CSV's header write does real work). Considered writing the closing `]` from inside `FlushAsync` instead, reusing the fact that `ProcessAsync` already calls it exactly once after the loop — rejected because it overloads `FlushAsync`'s name (stream flush only) with unrelated framing responsibility.

```csharp
internal interface IRecordWriter : IDisposable, IAsyncDisposable
{
    ValueTask WriteHeaderAsync(CancellationToken ct);
    ValueTask WriteStartRecordAsync(CancellationToken ct);
    void WriteCellData(int outputColumnIndex, CellData cell);
    ValueTask WriteEndRecordAsync(CancellationToken ct);
    ValueTask WriteFooterAsync(CancellationToken ct); // new
    ValueTask FlushAsync(CancellationToken ct);
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/IRecordWriter.cs` | Add `WriteFooterAsync` |
| `src/App/Cli/RecordProcessor.cs` | Call `writer.WriteFooterAsync(ct)` after the loop, before `FlushAsync` |
| `src/App/Cli/CsvRecordWriter.cs` | Add no-op `WriteFooterAsync` |
| `src/App/Cli/JsonLinesRecordWriter.cs` | Add no-op `WriteFooterAsync` |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/CsvRecordWriterTests.cs` | Add case: `WriteFooterAsync` is a no-op |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordWriterTests.cs` | Add case: `WriteFooterAsync` is a no-op |
| `tests/Refedle.Tests/App/Cli/RecordProcessorTests.cs` (if it exists) | Add case: `WriteFooterAsync` is called once, after the loop and before `FlushAsync` |

#### `JsonCellWriter` (extracted from `JsonLinesRecordWriter`)

`JsonLinesRecordWriter.WriteCellData`'s body moves, unchanged, into a new static class `JsonCellWriter`, so `JsonArrayRecordWriter` can reuse the same cell-encoding logic without duplicating it. A pure static function rather than a field-holding struct/class: `IRecordWriterFactory<TWriter> where TWriter : struct` already rules out a `class`, and between the two zero-allocation options left (a struct field holding `outputSchema`, or a static function taking it as a parameter), the deciding factor is that `outputSchema` backs exactly one method (`WriteCellData`) — with no second method to share the field with, holding it as state buys no real coupling reduction over passing it as an argument. Consistent with existing static-class patterns in this codebase (e.g. `DrillDownSchemaExtractor`) and this project's Pure Functions guideline.

```csharp
internal static class JsonCellWriter
{
    public static void WriteCellData(Utf8JsonWriter writer, BatchOutputSchema outputSchema, int outputColumnIndex, CellData cell);
    // Body moved verbatim from JsonLinesRecordWriter.WriteCellData, including the local writeNumericValue function.
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/JsonCellWriter.cs` | New |
| `src/App/Cli/JsonLinesRecordWriter.cs` | Replace `WriteCellData` body with a call to `JsonCellWriter.WriteCellData(_jsonWriter, _outputSchema, outputColumnIndex, cell)` |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/JsonCellWriterTests.cs` | New — covers the cell-encoding branches moved out of `JsonLinesRecordWriterTests` |

#### `JsonArrayRecordWriter`

Same `Utf8JsonWriter`/`PooledBufferWriter` construction as `JsonLinesRecordWriter`. Unlike JSON Lines (one self-contained object per line, reset per record), the array's framing spans records: `WriteHeaderAsync` opens `[`, `WriteStartRecordAsync` writes a leading `,` for every record after the first then `{`, `WriteEndRecordAsync` writes `}`, and the new `WriteFooterAsync` closes `]`.

```csharp
internal struct JsonArrayRecordWriter : IRecordWriter
{
    private readonly BatchOutputSchema _outputSchema;
    private bool _isFirstRecord;
    // stream / PooledBufferWriter / Utf8JsonWriter fields as in JsonLinesRecordWriter

    public readonly ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        // write '['
    }

    public ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        // if (!_isFirstRecord) write ','; _isFirstRecord = false; write '{'
    }

    public readonly void WriteCellData(int outputColumnIndex, CellData cell) =>
        JsonCellWriter.WriteCellData(_jsonWriter, _outputSchema, outputColumnIndex, cell);

    public readonly ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        // write '}', flush this record's bytes to the stream
    }

    public readonly ValueTask WriteFooterAsync(CancellationToken ct)
    {
        // write ']'
    }

    public readonly ValueTask FlushAsync(CancellationToken ct) => _stream is null ? default : new(_stream.FlushAsync(ct));
}

[RecordWriter(DataFormat.JsonArray)]
internal readonly struct JsonArrayRecordWriterFactory : IRecordWriterFactory<JsonArrayRecordWriter>
{
    public ValueTask<JsonArrayRecordWriter> CreateAsync(string outputFile, BatchOutputSchema outputSchema, IAppLogger logger, CancellationToken ct);
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/JsonArrayRecordWriter.cs` | New |
| `src/App/Cli/Factories/JsonArrayRecordWriterFactory.cs` | New |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/JsonArrayRecordWriterTests.cs` | New — covers empty-array (`[]`), single-record, and multi-record (comma placement) output shapes |

### Phase 5: Wire `DrillDownKeyPath` from Runner to Reader Factory

`ActionApplier.BuildOutputSchema` is already called unconditionally from `Runner.cs`, format-agnostic, and `RecordProcessor.ProcessAsync` is already fully generic over any `IRecordReader`/`IRecordWriter` — so once the DrillDown-scoped rows extracted in Phase 3 flow into the pipeline, recipe Actions apply to them automatically with no new code in either. What remains is wiring: `recipe.DrillDownKeyPath` (`IReadOnlyList<KeyPathSegment>?`, `src/Engine/Models/Recipe.cs`) needs to travel from `Runner.cs` through `ColumnNameResolver.ResolveColumnNames` and `FormatDispatcher.DispatchAsync` down to each reader factory's `CreateAsync` — both already have a `drillDownKeyPath` parameter as of Phase 2, but Runner passes `null` until this phase. The writer side never needs it (Phase 2 confirmed).

```csharp
// Runner.cs
var columnNames = ColumnNameResolver.ResolveColumnNames(
    inputFormat, args.InputFile, recipe.DrillDownKeyPath, ct); // was: null-equivalent (Phase 2 stub)

...

return await Generated.FormatDispatcher.DispatchAsync(
    inputFormat, outputFormat, args.InputFile, args.OutputFile,
    recipe.DrillDownKeyPath, columnNames, outputSchema, logger, ct).ConfigureAwait(false);
```

```csharp
internal static class FormatDispatcher
{
    public static async ValueTask<ExitCode> DispatchAsync(
        DataFormat inputFormat,
        DataFormat outputFormat,
        string inputFile,
        string outputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath, // new
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        return (inputFormat, outputFormat) switch
        {
            (DataFormat.Csv, DataFormat.Csv) =>
                await RunCsvToCsvAsync(inputFile, outputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct),
            // ... one arm per reader×writer pair, all following the same shape
        };
    }

    // One representative example — mechanically identical for every other pair.
    private static async ValueTask<ExitCode> RunCsvToCsvAsync(
        string inputFile,
        string outputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath, // new — forwarded to reader factory only
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        var readerFactory = new CsvRecordReaderFactory();
        using var reader = await readerFactory.CreateAsync(inputFile, drillDownKeyPath, inputColumnNames, outputSchema, logger, ct).ConfigureAwait(false);

        var writerFactory = new CsvRecordWriterFactory();
        await using var writer = await writerFactory.CreateAsync(outputFile, outputSchema, logger, ct).ConfigureAwait(false); // no drillDownKeyPath

        return await RecordProcessor.ProcessAsync<CsvRecordReader, CsvRecordWriter>(reader, writer, outputSchema.Columns, ct).ConfigureAwait(false);
    }
}
```

**Affected Files**

| File | Change |
|---|---|
| `src/App/Cli/Runner.cs` | Pass `recipe.DrillDownKeyPath` (instead of `null`) to `ColumnNameResolver.ResolveColumnNames` and to `Generated.FormatDispatcher.DispatchAsync` |
| `src/Generators/FormatDispatcherGenerator.cs` | Add `drillDownKeyPath` parameter to generated `DispatchAsync` and each `Run{Reader}To{Writer}Async`; forward it into the reader factory's `CreateAsync` call only (writer factory call unchanged) |

**Unit Tests**

| File | Change |
|---|---|
| `tests/Refedle.Tests/Generators/FormatDispatcherGeneratorTests.cs` | Update expected generated source strings to include the new parameter |
| `tests/Refedle.Tests/App/Cli/RunnerTests.cs` | Add/update test case(s) verifying `recipe.DrillDownKeyPath` reaches `ColumnNameResolver`/`FormatDispatcher.DispatchAsync` |

### Phase 6: Extend DrillDown to JSON Lines Input

Apply Full Aggregation DrillDown to JSON Lines input, fixing the current silent bug where `Runner.cs` ignores `recipe.DrillDownKeyPath` for JSON Lines and applies the recipe Actions against the KeyPath-less base schema. Phase 5 already wires `drillDownKeyPath` from `Runner` through to the reader factory and `ColumnNameResolver` (unused for JSON Lines until now); Phase 6 consumes it.

Bare (non-DrillDown) and Full Aggregation DrillDown reading are held in a single dispatch struct (`JsonLinesRecordReader`) that carries both and selects one at construction time. See ADR-6 for why.

#### Bare reader rename

| From | To |
|---|---|
| `src/App/Cli/JsonLinesRecordReader.cs` | `src/App/Cli/BareJsonLinesRecordReader.cs` (logic unchanged) |
| `src/App/Cli/JsonLinesRecordReader.PooledValueBuffer.cs` | `src/App/Cli/BareJsonLinesRecordReader.PooledValueBuffer.cs` |

#### `JsonLinesRecordReader` (dispatch struct)

```csharp
using Refedle.Engine.IO.JsonLines;

namespace Refedle.App.Cli;

// [RecordReader(DataFormat.JsonLines)] binds to exactly one reader type, so the bare and
// DrillDown paths share this struct. The active path is fixed at construction time.
internal struct JsonLinesRecordReader : IRecordReader
{
    private readonly bool _isDrillDown;
    private BareJsonLinesRecordReader _bare;
    private FullAggregationRecordReader<JsonLinesBatchSourceReader> _drillDown;

    // Neither path selected — exists only so the two real constructors can chain
    // `: this()` and skip zeroing the unused field.
    private JsonLinesRecordReader()
    {
    }

    public JsonLinesRecordReader(BareJsonLinesRecordReader bare) : this()
    {
        _isDrillDown = false;
        _bare = bare;
    }

    public JsonLinesRecordReader(FullAggregationRecordReader<JsonLinesBatchSourceReader> drillDown) : this()
    {
        _isDrillDown = true;
        _drillDown = drillDown;
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct) =>
        _isDrillDown ? _drillDown.MoveNextAsync(ct) : _bare.MoveNextAsync(ct);

    public bool EvaluateFilters() =>
        _isDrillDown ? _drillDown.EvaluateFilters() : _bare.EvaluateFilters();

    public CellData GetCellData(int outputColumnIndex) =>
        _isDrillDown ? _drillDown.GetCellData(outputColumnIndex) : _bare.GetCellData(outputColumnIndex);

    public void Dispose()
    {
        if (_isDrillDown)
        {
            _drillDown.Dispose();
            return;
        }

        _bare.Dispose();
    }
}
```

#### `JsonLinesBatchSourceReader`

A thin struct adapting `FullAggregationRecordReader<TBatchSourceReader>` (Phase 3) to JSON Lines batch reads. `RowReader.ReadLines(long, int, int)` matches `IBatchSourceReader.ReadBatch(long, int, int)`.

Unlike `JsonArrayBatchSourceReader` (Phase 3), it holds a nullable `RowReader?`. Zero-record JSON Lines input is a zero-byte file, which `MmapService.Open` rejects (`fileInfo.Length == 0`), so `RowReader` cannot be constructed. Zero-element JSON Array is `[]` (2 bytes) and `ElementReader` always constructs — this asymmetry is deliberate.

A `ReadBatch` after disposal throws via `RowReader`'s own `ObjectDisposedException` guard (the same way `JsonArrayBatchSourceReader` relies on `ElementReader`'s guard). `_reader` is not nulled out, so the `?? []` only applies to the zero-record case.

```csharp
using Refedle.Engine.IO.JsonLines;

namespace Refedle.App.Cli;

internal readonly struct JsonLinesBatchSourceReader(RowReader? reader) : IBatchSourceReader
{
    private readonly RowReader? _reader = reader;

    public IReadOnlyList<JsonRawBytes> ReadBatch(long byteOffset, int skip, int fetch) =>
        _reader?.ReadLines(byteOffset, skip, fetch) ?? [];

    public void Dispose() => _reader?.Dispose();
}
```

#### `JsonLinesRecordReaderFactory`

```csharp
using Refedle.Engine.IO.JsonLines;

[RecordReader(DataFormat.JsonLines)]
internal readonly struct JsonLinesRecordReaderFactory : IRecordReaderFactory<JsonLinesRecordReader>
{
    public ValueTask<JsonLinesRecordReader> CreateAsync(
        string inputFile,
        IReadOnlyList<KeyPathSegment>? drillDownKeyPath,
        IReadOnlyList<string> inputColumnNames,
        BatchOutputSchema outputSchema,
        IAppLogger logger,
        CancellationToken ct)
    {
        var rowIndexer = new RowIndexer(inputFile);
        rowIndexer.BuildIndex(CancellationToken.None);

        // RowReader must not be constructed for zero-record input (MmapService rejects empty files).
        var rowReader = rowIndexer.TotalRows == 0 ? null : new RowReader(inputFile);

        if (drillDownKeyPath is null)
        {
            return new(new JsonLinesRecordReader(
                new BareJsonLinesRecordReader(rowIndexer, rowReader, inputColumnNames, outputSchema)));
        }

        // Wraps RowReader in JsonLinesBatchSourceReader, constructs FullAggregationRecordReader.
        return new(new JsonLinesRecordReader(
            new FullAggregationRecordReader<JsonLinesBatchSourceReader>(
                rowIndexer, new JsonLinesBatchSourceReader(rowReader), drillDownKeyPath, outputSchema)));
    }
}
```

`FullAggregationRecordReader<TBatchSourceReader>`'s constructor parameters are finalized when Phase 3 is implemented. Zero-record input is handled by the contract that it yields no rows (and never calls `ReadBatch`) when `RowIndexerBase.TotalRows == 0`.

#### `ColumnNameResolver`

Add a `drillDownKeyPath` branch to the `DataFormat.JsonLines` arm:

```csharp
DataFormat.JsonLines => drillDownKeyPath is null
    ? ResolveJsonLinesColumnNames(inputFile, ct)
    : ResolveFullAggregationColumnNames(inputFile, DataFormat.JsonLines, drillDownKeyPath, ct),
```

`ResolveFullAggregationColumnNames` is the helper added for `JsonArray` in Phase 3 (calls `FullAggregationSchemaScanner.Scan`, switching on the `format` argument) — reused as-is.

#### Unchanged

- **`DrillDownRecipeValidator`**: JSON Lines is valid whether `drillDownKeyPath` is `null` (bare) or non-`null` (DrillDown). Phase 3's check (`JsonObject`/`JsonArray` + `null` keyPath) does not cover JSON Lines.
- **`FormatDispatcherGenerator`**: `JsonLinesRecordReaderFactory` still implements a single `IRecordReaderFactory<JsonLinesRecordReader>`. The reader type the generator sees is unchanged, and the generated source is unchanged.

#### Affected Files

| File | Change |
|---|---|
| `src/App/Cli/JsonLinesRecordReader.cs` | Replace contents with the new dispatch struct |
| `src/App/Cli/BareJsonLinesRecordReader.cs` | New (renamed from `JsonLinesRecordReader.cs`, logic unchanged) |
| `src/App/Cli/BareJsonLinesRecordReader.PooledValueBuffer.cs` | New (renamed) |
| `src/App/Cli/JsonLinesRecordReader.PooledValueBuffer.cs` | Deleted (moved) |
| `src/App/Cli/JsonLinesBatchSourceReader.cs` | New |
| `src/App/Cli/Factories/JsonLinesRecordReaderFactory.cs` | Branch on `drillDownKeyPath`; add `using Refedle.Engine.IO.JsonLines;` |
| `src/App/Cli/ColumnNameResolver.cs` | Add a `drillDownKeyPath` branch to the `JsonLines` arm |
| `src/App/GlobalSuppressions.cs` | Update any `Target` strings referencing `JsonLinesRecordReader` |
| `benchmarks/Refedle.Benchmarks/App/Cli/JsonLinesRecordReaderBenchmarks.cs` | Point at `BareJsonLinesRecordReader` (keeps the bare-path benchmark) |
| `benchmarks/Refedle.Benchmarks/GlobalSuppressions.cs` | Update the two `Target` strings referencing `JsonLinesRecordReaderBenchmarks` |

#### Unit Tests

| File | Change |
|---|---|
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.cs` → `BareJsonLinesRecordReaderTests.cs` | Rename; `new JsonLinesRecordReader(...)` → `new BareJsonLinesRecordReader(...)` (constructs the struct directly, assertions unchanged) |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.cs` | New — dispatch contract: `[Theory]` over `bool drillDown`, exercising every `IRecordReader` member in both modes |
| `tests/Refedle.Tests/App/Cli/JsonLinesBatchSourceReaderTests.cs` | New |
| `tests/Refedle.Tests/App/Cli/FullAggregationRecordReaderTests.JsonLinesBatchSourceReader.cs` | New (pairs with the Phase 3 `.JsonArrayBatchSourceReader.cs`) |
| `tests/Refedle.Tests/Engine/IO/DrillDown/FullAggregationSchemaScannerTests.cs` | Add JSON Lines input cases |
| `tests/Refedle.Tests/App/Cli/ColumnNameResolverTests.cs` | Add a `JsonLines` + DrillDown case |
| `tests/Refedle.Tests/Generators/FormatDispatcherGeneratorTests.cs` | No change (asserts the generated source is unchanged) |

### Phase 7: E2E Tests

Run the CLI batch pipeline through the real binary and verify that every one of the 15 Input × Output pairs in the coverage matrix exits 0 and writes the expected output. Every case uses the same single Filter action, keeping the test's focus on "the Input × Output pairing works" — per-action coverage stays the job of the existing `*OutputTests.<Action>Action.cs` files. DrillDown-input rows set `drillDownKeyPath` in the recipe, and the Filter targets a column of the drilled-down table.

Follows the existing naming convention: partial files are split by action (`FilterAction.cs`), and the input/output pair is encoded in the method name `Run_{Input}To{Output}_WithFilterAction_{expectation}`.

**Coverage matrix** (each method lives in its output group's `*.FilterAction.cs`)

| Input | Output | Method | Status |
|---|---|---|---|
| CSV | CSV | `Run_CsvToCsv_WithFilterAction_*` | ✅ existing |
| CSV | JSON Lines | `Run_CsvToJsonLines_WithFilterAction_*` | ✅ existing |
| CSV | JSON Array | `Run_CsvToJson_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Lines (bare) | CSV | `Run_JsonLinesToCsv_WithFilterAction_*` | ✅ existing |
| JSON Lines (bare) | JSON Lines | `Run_JsonLinesToJsonLines_WithFilterAction_*` | ✅ existing |
| JSON Lines (bare) | JSON Array | `Run_JsonLinesToJson_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Lines (Full Aggregation) | CSV | `Run_JsonLinesDrillDownToCsv_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Lines (Full Aggregation) | JSON Lines | `Run_JsonLinesDrillDownToJsonLines_WithFilterAction_*` | 🔲 Phase 7 (bug-fix centerpiece) |
| JSON Lines (Full Aggregation) | JSON Array | `Run_JsonLinesDrillDownToJson_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Array (Full Aggregation) | CSV | `Run_JsonArrayDrillDownToCsv_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Array (Full Aggregation) | JSON Lines | `Run_JsonArrayDrillDownToJsonLines_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Array (Full Aggregation) | JSON Array | `Run_JsonArrayDrillDownToJson_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Object (Single) | CSV | `Run_JsonObjectDrillDownToCsv_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Object (Single) | JSON Lines | `Run_JsonObjectDrillDownToJsonLines_WithFilterAction_*` | 🔲 Phase 7 |
| JSON Object (Single) | JSON Array | `Run_JsonObjectDrillDownToJson_WithFilterAction_*` | 🔲 Phase 7 (ADR-1: a single row is still `[{…}]`) |

**Affected Files**

| File | Change |
|---|---|
| `tests/Refedle.E2ETests/Cli/Output/Csv/CsvOutputTests.FilterAction.cs` | Add `Run_JsonLinesDrillDownToCsv_*`, `Run_JsonArrayDrillDownToCsv_*`, `Run_JsonObjectDrillDownToCsv_*` |
| `tests/Refedle.E2ETests/Cli/Output/JsonLines/JsonLinesOutputTests.FilterAction.cs` | Add `Run_JsonLinesDrillDownToJsonLines_*`, `Run_JsonArrayDrillDownToJsonLines_*`, `Run_JsonObjectDrillDownToJsonLines_*` |
| `tests/Refedle.E2ETests/Cli/Output/Json/JsonOutputTests.cs` | New — partial-class fixture for the JSON output group (mirrors `CsvOutputTests.cs` / `JsonLinesOutputTests.cs`) |
| `tests/Refedle.E2ETests/Cli/Output/Json/JsonOutputTests.FilterAction.cs` | New — `Run_CsvToJson_*`, `Run_JsonLinesToJson_*`, `Run_JsonLinesDrillDownToJson_*`, `Run_JsonArrayDrillDownToJson_*`, `Run_JsonObjectDrillDownToJson_*` |

## Architecture Decision Log

### ADR-1: Always emit a JSON Array, regardless of row count

**Context**

How should the top-level JSON shape (Object vs. Array) of the output be decided?

**Options**

- **A — Decide statically from DrillDown kind (Single → Object, Full Aggregation → Array)**
  - Rejected: "Single" only means the target node is a single node, not that the output is a single row. A Single DrillDown's target node can itself hold multiple elements, so its output can still be multiple rows — a single JSON Object cannot represent that.
- **B — Decide dynamically from the actual row count (unwrap to a bare JSON Object when there's exactly one row, otherwise emit a JSON Array)**
  - Rejected: an output shape whose top-level type (Object vs. Array) flips depending on element count is a known anti-pattern — it forces downstream consumers to branch on type, or breaks their parsing outright. This has repeatedly caused real problems in XML-to-JSON conversion tools.
- **C (Adopted) — Always emit a JSON Array**, regardless of row count or DrillDown kind.

**Rationale**

- The output format's structure (its top-level type) should stay constant regardless of the data's actual content (row count).
- Letting the shape depend on row count reintroduces the exact problem Option B was rejected for — forcing consumers to branch on type.

**Consequence**

- A Single DrillDown result is still wrapped in an array even when it has exactly one row (`[{...}]`). Callers must expect this.

### ADR-2: `DrillDownRecipeValidator` as an independent, standalone class

**Context**

Where should the check "input is JSON Array/Object but `recipe.DrillDownKeyPath` is missing" live?

**Options**

- **A — Fold it into `ColumnNameResolver`, converting it to return `Result<T>`**
  - Rejected: mixing validation responsibility into column-name resolution overloads that class's responsibility.
- **B (Adopted) — A new, independent `DrillDownRecipeValidator`**, taking only `Recipe` and `DataFormat` and returning a `Result`.

**Rationale**

- Keeps a single, easy-to-understand responsibility.
- `RecipeManager.LoadAsync` and `FormatDetector.DetectInputFile` are already standalone; a future `--dry-run` command can reuse all three the same way.

**Consequence**

- Adds one more call site in `Runner.cs` (immediately before `ColumnNameResolver`).
- Phase 3 makes `ColumnNameResolver` return `Result<T>`, which is not a reversal of Option A: the applicability check still lives in the standalone `DrillDownRecipeValidator`; the `Result` only carries the `JsonObject` branch's own resolution failures.

### ADR-3: New streaming design for `FullAggregationRecordReader`

**Context**

Row extraction for Full Aggregation DrillDown (JSON Array/JSON Lines). The existing `FullAggregationScanner`, used by the TUI, materializes every matching row into a `List<FocusedTableRow>` in one call.

**Options**

- **A — Reuse `FullAggregationScanner` as-is**
  - Rejected: a CLI batch run must not hold every row in memory regardless of file size.
- **B (Adopted) — New streaming design.** Two new public Engine-layer wrappers (`FullAggregationSchemaScanner.Scan`, `FullAggregationRowExtractor.ExtractRows`). The App layer streams through `IBatchSourceReader`, reading a bounded batch at a time.

**Rationale**

- Streaming a bounded batch at a time keeps memory usage minimal regardless of file size.

**Consequence**

- Adds several new classes (two in Engine, three in App). Duplicates part of `FullAggregationScanner`'s logic — TUI and CLI end up with separate implementations, a trade-off accepted here. Unifying the TUI onto this streaming approach is a future goal, out of scope for now.

### ADR-4: Single DrillDown reuses the existing resolve/extract logic — no new streaming infrastructure

**Context**

Row extraction for Single DrillDown (JSON Object). Whether a new streaming infrastructure, like the one built for Full Aggregation, is needed here too.

**Options**

- **A — Build a new streaming infrastructure, same as Full Aggregation**
  - Rejected: the target is always a single resolved node, not the whole file — over-engineering for a bounded target.
- **B (Adopted) — Reuse the existing `KeyPathNodeResolver.ResolveSingleNode` + `DrillDownSchemaExtractor.ExtractFromNode` unchanged.**

**Rationale**

- The target node is always a single one, so the data that needs to be held is bounded to that node's worth — reading it once and holding that is already sufficient. No batch-oriented streaming infrastructure, like Full Aggregation's, is needed.

**Consequence**

- Single DrillDown and Full Aggregation DrillDown end up with asymmetric implementation approaches (reuse existing vs. new streaming), a deliberate difference stemming from their underlying data-shape difference (one bounded node vs. a full scan).

### ADR-5: `JsonCellWriter` as a pure static function

**Context**

Cell-writing logic (extracted from `JsonLinesRecordWriter.WriteCellData` for reuse by `JsonArrayRecordWriter`) needs `outputSchema` to write a cell. The question is where that shared logic should live, and whether `outputSchema` should be held as state. `writer`, `outputColumnIndex`, and `cell` are already per-call values passed as arguments regardless of design — only `outputSchema` is a genuine candidate for being held as instance state instead.

**Options**

- **A — Share the logic by having `JsonLinesRecordWriter` and `JsonArrayRecordWriter` inherit a common base type**
  - Rejected: both writers must be `struct` (each is the `TWriter` of an `IRecordWriterFactory<TWriter> where TWriter : struct`), and structs cannot inherit. This is the root reason the shared logic has to live in a separate type at all.
- **B — Introduce a class/struct that holds `outputSchema` as a field**
  - Rejected: `JsonCellWriter` has exactly one method (`WriteCellData`), so there is no scenario where state would be shared across multiple methods — no reason to single out `outputSchema` as field state.
- **C (Adopted) — `JsonCellWriter` as a static class, `WriteCellData` as a pure function** taking `outputSchema` as an argument alongside `writer`, `outputColumnIndex`, and `cell` — no instance state at all.

**Rationale**

- One method only — no need to share state across calls.
- Consistent with existing static-class patterns in this codebase (e.g. `DrillDownSchemaExtractor`).

**Consequence**

- Both `JsonLinesRecordWriter` and `JsonArrayRecordWriter` call `JsonCellWriter.WriteCellData(...)`, eliminating the cell-writing logic duplication between them.

### ADR-6: A single union dispatch struct for the bare and DrillDown JSON Lines readers

**Context**

`[RecordReader(DataFormat.JsonLines)]` can bind to only one concrete type (`FormatDispatcherGenerator` generates dispatch assuming one reader type per `DataFormat`). The bare reader (non-DrillDown, the existing straight-line one-line-per-row read) and Full Aggregation DrillDown (one record → 0..N rows, via `KeyPathTraverser`'s DFS) are genuinely different `IRecordReader` implementations. They have to share one slot.

**Options**

- **A — Delete the bare reader; route all JSON Lines reads through `FullAggregationRecordReader<JsonLinesBatchSourceReader>`**, passing `drillDownKeyPath ?? []` so the non-DrillDown case runs the same path with an empty KeyPath.
  - Rejected: even with an empty KeyPath, every non-DrillDown conversion then pays the DrillDown machinery's per-record cost (a `Stack<TraversalFrame>` allocation and the traversal/leaf-collection path) and depends on `KeyPathTraverser`.
- **B (Adopted) — A union dispatch struct.** `JsonLinesRecordReader` holds both a `BareJsonLinesRecordReader` and a `FullAggregationRecordReader<JsonLinesBatchSourceReader>` as fields, with a `bool _isDrillDown` fixed at construction; each `IRecordReader` member forwards to the selected path.
- **C — Strategy pattern:** `JsonLinesRecordReader` holds a `private readonly IRecordReader _inner`, assigned at construction, and every method calls `_inner.X()` unconditionally.
  - Rejected: assigning a struct into an interface-typed field boxes it, and every per-row / per-cell `MoveNextAsync` / `GetCellData` becomes a virtual call. The pipeline is built to stay monomorphized through `IRecordReaderFactory<TReader> where TReader : struct`; this would break that for JSON Lines only.
- **D — Extend `FormatDispatcherGenerator`** with a `DrillDown` axis on `[RecordReader(DataFormat)]`, threading a runtime `isDrillDown` through `FormatInfo`, the generated method names, and the `DispatchAsync` switch.
  - Rejected: touches the shared generator (and the exact-match generated-source tests set up in Phase 1), with impact across every reader×writer combination, not just JSON Lines.
- **E — A dedicated source generator** that emits the union forwarding struct from the two implementations, removing the hand-written per-member forwarding.
  - Deferred, not rejected: a second generator plus its exact-match test infrastructure is disproportionate for one dispatch struct. The hand-written forwarding is small and its correctness is covered by the two-mode dispatch contract test. Revisit if the same pattern is needed for other formats.

**Rationale**

- Keeps the bare reader's straight-line, zero-allocation read path intact for every existing non-DrillDown JSON Lines conversion.
- Accepts the trade-off that each `IRecordReader` member needs an `_isDrillDown` branch.

**Consequence**

- Every `JsonLinesRecordReader` member is `_isDrillDown ? _drillDown.X() : _bare.X()` (`Dispose` as a guard clause). A member added later without the branch would silently run the wrong path — covered by a dispatch contract test parametrized over both modes.
- The forwarding could later be source-generated (Option E); out of scope here.

### ADR-7: Typed cell extraction as a static function taking `PooledValueBuffer`

**Context**

Three readers — the existing `JsonLinesRecordReader`, `JsonObjectRecordReader`, and `FullAggregationRecordReader` — decode a typed `CellData` from one JSON object's bytes by column name into a pooled `char[]`. The logic is currently inline in `JsonLinesRecordReader.GetCellData`. Where the shared copy lives, and who owns the `PooledValueBuffer`.

**Options**

- **A — A `struct` holding `PooledValueBuffer` as a field**
  - Rejected: a `readonly struct` cannot reassign the `char[]` on growth, and a mutable one would double-rent / double-return the pool buffer on every struct copy — `PooledValueBuffer` is already a `class` for this same reason.
- **B — A `class` that owns the buffer**
  - Rejected: inserts a second owning type between the reader and the buffer; the reader already owns and disposes the buffer directly.
- **C (Adopted) — `JsonObjectCellReader` as a static class**, `ReadCell(objectBytes, columnNameUtf8, PooledValueBuffer)` — buffer ownership stays with each reader.

**Rationale**

- Buffer ownership is unchanged; only the ~90-line walk is shared.
- Same shape as ADR-5 (a static function with the state passed as an argument).

**Consequence**

- `PooledValueBuffer` is promoted from a private nested type of `JsonLinesRecordReader` to a standalone `internal sealed class`.
