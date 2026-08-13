# Design: FilterAction — Row-Level Filtering for CSV and JsonLines Table Mode

## Overview

Add `FilterAction` as a new `MorphAction` subtype that enables where-clause-style row-level filtering in CSV and JsonLines table modes. Users specify a condition on a column (operator + value), and only rows satisfying **all** active filters are displayed via `LazyTransformer`. Filter conditions are persisted as part of the Recipe for headless batch processing.

---

## Requirements

- Filter rows in CSV and JsonLines table mode based on a column value condition.
- Conditions are specified as: `<column> <operator> <value>`.
- Multiple `FilterAction`s in the action stack are applied with **AND semantics**.
- Filtering is lazy from the TUI's perspective: `LazyTransformer` builds the filtered row index at construction time and exposes a reduced `Rows` count.
- `FilterAction` is serialized into `Recipe.Actions` the same as other `MorphAction` subtypes (JSON polymorphism via `System.Text.Json` Source Generators, AOT-safe).

---

## New Types

### `FilterOperator` enum

**File**: `src/Engine/Models/Actions/FilterOperator.cs`

```
Equals           ==        All column types (raw string comparison for Text)
NotEquals        !=        All column types
GreaterThan      >         WholeNumber, FloatingPoint, Timestamp
LessThan         <         WholeNumber, FloatingPoint, Timestamp
GreaterThanOrEqual >=      WholeNumber, FloatingPoint, Timestamp
LessThanOrEqual  <=        WholeNumber, FloatingPoint, Timestamp
Contains         contains  Text
NotContains      not contains  Text
StartsWith       starts with   Text
EndsWith         ends with     Text
```

### `FilterAction` record

**File**: `src/Engine/Models/Actions/FilterAction.cs`

- Inherits from `MorphAction`
- Type discriminator: `"filter"`
- Properties:
  - `required string ColumnName`
  - `required FilterOperator Operator`
  - `required string Value`
- `Description`: `"Filter '{ColumnName}' {Operator} '{Value}'"`

Registration in `MorphAction.cs`:

```csharp
[JsonDerivedType(typeof(FilterAction), typeDiscriminator: "filter")]
```

---

## `LazyTransformer` Changes

### New private type: `FilterSpec`

```csharp
private readonly record struct FilterSpec(
    int SourceColumnIndex,
    ColumnType ColumnType,
    FilterOperator Operator,
    string Value
);
```

Resolved at construction: maps `FilterAction.ColumnName` → `sourceColumnIndex` + effective `ColumnType` at that point in the action stack (respecting preceding `CastColumnAction`s).

### `BuildTransformedSchema` extension

The existing method is extended to also collect `FilterSpec`s as it iterates through the action stack:

- When a `FilterAction` is encountered, look up `ColumnName` in the current `nameToIndex` dictionary.
- If found, record a `FilterSpec(sourceIndex, currentType, operator, value)`.
- If not found (column was deleted), silently skip.
- `FilterAction` does **not** modify the column schema (it is a row-level operation).

Return type extended to include `IReadOnlyList<FilterSpec>`.

### `FilterRowIndexer` reference

```csharp
private readonly IFilterRowIndexer? _filterRowIndexer;
```

`null` when no `FilterSpec`s exist (no allocation, full row passthrough). When filters are present, holds an `IFilterRowIndexer` that asynchronously builds a list of matching source row indices. The index is built in a background task started by the caller after `LazyTransformer` construction.

### Updated `Rows` property

```csharp
public int Rows => _filterRowIndexer is not null ? _filterRowIndexer.TotalMatchedRows : _source.Rows;
```

`TotalMatchedRows` increments atomically (via `Interlocked`) as the background scan progresses, mirroring the existing `DataRowIndexer.TotalRows` pattern. The TUI observes a growing row count until the scan completes.

### Updated indexer

```csharp
var sourceRow = _filterRowIndexer is not null ? _filterRowIndexer.GetSourceRow(row) : row;
if (sourceRow < 0) return string.Empty; // row not yet scanned
var rawValue = _source[sourceRow, sourceCol]?.ToString() ?? string.Empty;
```

`GetSourceRow` returns `-1` for rows beyond the current scan progress; the TUI renders an empty cell in that case.

### Filter evaluation logic

Extracted into a private static method `EvaluateFilter(string rawValue, FilterSpec spec) -> bool`:

| Operator | Evaluation |
|----------|-----------|
| `Equals` | `string.Equals(rawValue, spec.Value, StringComparison.OrdinalIgnoreCase)` |
| `NotEquals` | negation of Equals |
| `Contains` | `rawValue.Contains(spec.Value, StringComparison.OrdinalIgnoreCase)` |
| `NotContains` | negation of Contains |
| `StartsWith` | `rawValue.StartsWith(spec.Value, StringComparison.OrdinalIgnoreCase)` |
| `EndsWith` | `rawValue.EndsWith(spec.Value, StringComparison.OrdinalIgnoreCase)` |
| `GreaterThan / LessThan / ...` | Parse `rawValue` and `spec.Value` as `long` (WholeNumber), `double` (FloatingPoint), or `DateTime` (Timestamp). If parsing fails → row excluded. |

For numeric/Timestamp operators applied to `Text` columns: fall back to `Equals` / `NotEquals` string comparison (operator semantics degrade gracefully).

---

## `IFilterRowIndexer` and Format-Specific Implementations

### Interface

**File**: `src/App/Views/IFilterRowIndexer.cs`

```csharp
internal interface IFilterRowIndexer
{
    /// <summary>
    /// Number of source rows confirmed to match all filters so far.
    /// Updated atomically; safe to read from the UI thread while BuildIndexAsync runs.
    /// </summary>
    int TotalMatchedRows { get; }

    /// <summary>
    /// Returns the source row index for a given filtered row index.
    /// Returns -1 if the scan has not yet reached this filtered row.
    /// </summary>
    int GetSourceRow(int filteredRow);

    /// <summary>
    /// Scans all source rows sequentially and builds the matched-row index.
    /// Must be called once on a background task after construction.
    /// </summary>
    Task BuildIndexAsync(CancellationToken ct);
}
```

### Concrete implementations

| Class | File | IO classes used |
|---|---|---|
| `CsvFilterRowIndexer` | `src/App/Views/CsvFilterRowIndexer.cs` | `DataRowIndexer`, `DataRowReader` |
| `JsonLinesFilterRowIndexer` | `src/App/Views/JsonLinesFilterRowIndexer.cs` | `JsonLines.RowIndexer`, `JsonLines.RowReader`, `CellExtractor` |

Both implementations follow the same algorithm:

1. Obtain `TotalRows` from the completed row indexer.
2. Open a **dedicated** reader (separate from the display cache) to avoid polluting `RowByteCache` / `DataRowCache`.
3. Iterate source rows `0 .. TotalRows-1` sequentially.
4. For each row, extract the filter-column cell value and evaluate all `FilterSpec`s.
5. On match: append source row index to an internal `List<int>` (under a `Lock`) and `Interlocked.Increment` the matched-rows counter.
6. Yield every 1 000 rows (`await Task.Yield()`) to keep the UI thread responsive.

Using the raw IO classes (not `ITableSource`) avoids the sliding-window cache overhead during a full sequential scan, limiting total file I/O to approximately **one full pass** for filter index construction plus **display-only reads** thereafter.

### `Shift+G` during scan

While `BuildIndexAsync` is running, `Shift+G` jumps to the last **currently confirmed** matched row. This matches the existing UX where `TotalRows` grows incrementally and `Shift+G` reflects live progress.

---

## TUI Dialog: `FilterColumnDialog`

**File**: `src/App/Views/Dialogs/FilterColumnDialog.cs`

### Layout

```
┌─ Filter Column ──────────────────────────┐
│ Column: <columnName>                     │
│                                          │
│ Operator:  [OptionSelector<FilterOp>]    │
│                                          │
│ Value:     [TextField________________]   │
│                                          │
│              [OK]  [Cancel]              │
└──────────────────────────────────────────┘
```

### Constructor

```csharp
internal FilterColumnDialog(string columnName)
```

All operators are shown (no type filtering in the dialog). Mismatched operator/type combinations are handled gracefully by `LazyTransformer.EvaluateFilter`.

### Output properties

- `bool Confirmed`
- `FilterOperator? SelectedOperator`
- `string? Value`

---

## Key Binding: `Shift+F`

Added to both `CsvTableView.OnKeyDown` and `JsonLinesTableView.OnKeyDown`:

```csharp
if (key.KeyCode == (KeyCode.F | KeyCode.ShiftMask))
{
    return HandleFilterColumn();
}
```

`HandleFilterColumn()` follows the same guard + dialog pattern as the existing `HandleRenameColumn()`, `HandleDeleteColumn()`, `HandleCastColumn()` methods:

1. Guard: `App`, `OnMorphAction`/`_onMorphAction`, `Table`, `SelectedColumn`
2. **Guard: row indexer BuildIndex must be complete** — if still scanning, return early (no dialog).
3. Open `FilterColumnDialog(columnName)`
4. On confirm: call `FilterAction.Create(ColumnName, Operator, ComparisonType, Value)`; the dialog already validated, so `IsFailure` here is unreachable — invoke the callback with `.Value`

### Why `Shift+F` is gated on BuildIndex completion

`FilterRowIndexer.BuildIndexAsync` uses the row indexer's `TotalRows` as the scan boundary. If `BuildIndex` has not finished, `TotalRows` is incomplete and the filter index would silently miss rows scanned afterwards. Disabling `Shift+F` until `BuildIndex` completes avoids this inconsistency without complex cross-task coordination.

In practice, `BuildIndex` completes well within user reaction time for files up to a few hundred MB (< 1 s on NVMe SSD). For very large files (e.g., 10 GB), the guard becomes meaningful and a status indicator (e.g., "Indexing…" in the status bar) should communicate the wait to the user.

---

## Recipe Serialization

`FilterAction` participates in `Recipe.Actions` (which is `IReadOnlyList<MorphAction>`) via `[JsonDerivedType]` registration on `MorphAction`. No additional changes to `Recipe.cs` are required. AOT-compatibility is maintained through the existing `JsonSourceGenerationContext`.

---

## Affected Files

| File | Change |
|------|--------|
| `src/Engine/Models/Actions/MorphAction.cs` | Add `[JsonDerivedType(typeof(FilterAction), "filter")]` |
| `src/Engine/Models/Actions/FilterOperator.cs` | New enum |
| `src/Engine/Models/Actions/FilterAction.cs` | New sealed record |
| `src/App/Views/IFilterRowIndexer.cs` | New interface |
| `src/App/Views/CsvFilterRowIndexer.cs` | New class — uses `DataRowIndexer` + `DataRowReader` |
| `src/App/Views/JsonLinesFilterRowIndexer.cs` | New class — uses `JsonLines.RowIndexer` + `JsonLines.RowReader` |
| `src/App/Views/LazyTransformer.cs` | Add `FilterSpec`, `IFilterRowIndexer?`, `EvaluateFilter`, update `Rows` and indexer |
| `src/App/Views/Dialogs/FilterColumnDialog.cs` | New dialog |
| `src/App/Views/CsvTableView.cs` | Add `Shift+F` → `HandleFilterColumn()` with BuildIndex guard |
| `src/App/Views/JsonLinesTableView.cs` | Add `Shift+F` → `HandleFilterColumn()` with BuildIndex guard |
| `tests/Refedle.Tests/App/Views/LazyTransformerTests.cs` | Add filter test cases |
| `tests/Refedle.Tests/Engine/Models/Actions/FilterActionTests.cs` | New test file |

---

## Test Cases

### `LazyTransformerTests` additions

- Single `FilterAction` with `Equals` → only matching rows returned
- Single `FilterAction` with `Contains` → substring match
- Multiple `FilterAction`s (AND semantics) → intersection
- `FilterAction` with no matching rows → `Rows == 0`
- `FilterAction` targeting a renamed column → correctly resolved
- `FilterAction` targeting a deleted column → silently skipped, no rows excluded
- `FilterAction` with numeric `GreaterThan` on `WholeNumber` column
- `FilterAction` with numeric operator on `Text` column → falls back to `Equals`/`NotEquals` string comparison

### `FilterActionTests`

- `Description` property format
- JSON round-trip serialization (all operators, verify discriminator `"filter"`)
- `FilterOperator` enum values serialize to correct string discriminators via `System.Text.Json`

---

## Architecture Decision Log

This section records the key design decisions made during the design phase, including the options considered and the rationale for each choice.

---

### ADR-1: Pre-built index vs. on-demand row evaluation

**Context**

The TUI needs to map "filtered row N" to a source row index efficiently. Two approaches were considered.

**Options**

- **A — Pre-built `int[]?` at construction**: scan all source rows synchronously in the `LazyTransformer` constructor and store matched indices.
- **B — On-demand evaluation**: evaluate filter conditions each time the TUI requests a row.

**Decision**: Option A is structurally required.

**Rationale**

Option A incurs an O(N) scan upfront when the filter is applied, but subsequent row access is O(1) (array index lookup). Option B avoids the upfront scan, but introduces two structural problems:

1. **Per-access scan cost**: each TUI row request must scan forward from the current position until the next matching row is found.
2. **`Shift+G` (jump to last row) is not implementable**: jumping to the last filtered row requires knowing the total matched-row count, which itself requires a full O(N) scan to the end of the file. Option B cannot provide this without performing the same full scan that option A does upfront.

Because `Shift+G` is a required TUI feature, the full O(N) scan is unavoidable. Option A performs that scan once at filter construction time and stores the result; option B defers the same cost to user interaction time. Option A is therefore structurally required.

Note: the memory cost of the index is modest — matched-row-count × 4 bytes.

---

### ADR-2: Synchronous vs. asynchronous index construction

**Context**

The pre-built index (ADR-1) blocks the constructor while scanning all source rows. For large files, this is unacceptable.

| Storage | 10 GB file read time |
|---|---|
| NVMe SSD | ~5 s |
| SATA SSD | ~20 s |
| HDD | ~2 min |

**Decision**: Build the index asynchronously in a background task.

**Rationale**

Synchronous construction blocks the constructor (and therefore the UI thread) for the entire duration of the scan. For large files this is unacceptable: users would see a frozen TUI for seconds to minutes. Asynchronous construction keeps the UI responsive and allows the TUI to render already-confirmed rows immediately, with `TotalMatchedRows` updated atomically (via `Interlocked`) as the scan progresses. This pattern — using `Interlocked` for atomic counter updates and `Lock` for list access — is already established in `DataRowIndexer` and `JsonLines.RowIndexer`, so no unfamiliar synchronization mechanisms are introduced. The UX trade-off — `Shift+G` jumping to the last currently confirmed matched row rather than the true final row during the scan — is acceptable and consistent with the existing incremental-index behaviour.

---

### ADR-3: `ITableSource` vs. raw IO classes for the filter scan

**Context**

`FilterRowIndexer.BuildIndexAsync` must read every source row to evaluate filter conditions. Two scan strategies were considered.

**Options**

- **A — Via `ITableSource`**: call `_source[row, col]` sequentially, relying on the existing sliding-window cache (`DataRowCache` / `RowByteCache`).
- **B — Via raw IO classes** (`DataRowIndexer`+`DataRowReader` / `JsonLines.RowIndexer`+`JsonLines.RowReader`): open a dedicated reader and scan sequentially, bypassing the display cache.

**Decision**: Option B.

**Rationale**

The display cache (`DataRowCache` / `RowByteCache`) holds a sliding window of ~200 rows. Under option A, the filter scan reads every source row sequentially via `_source[row, col]`, continuously overwriting the cache window. By the time the scan completes, the cache no longer contains the rows the user is currently viewing — TUI display then triggers a second round of file reads to reload them. The net result is that the file is read approximately twice: once for the filter scan, once for display.

Under option B, a dedicated `RowReader` reads the file directly without touching the display cache. The cache remains undisturbed throughout the scan, so TUI display after the scan requires only normal cache-miss reads. Total file I/O is limited to one sequential pass for filter-index construction plus display-only reads.

The trade-off is that option B requires implementing a new class per source data format (`CsvFilterRowIndexer` for CSV, `JsonLinesFilterRowIndexer` for JsonLines), whereas option A would work with a single generic implementation via `ITableSource`. This is acceptable given the existing format-specific IO layer.

---

### ADR-4: `Shift+F` gated on `BuildIndex` completion

**Context**

`FilterRowIndexer.BuildIndexAsync` uses `RowIndexer.TotalRows` as the scan boundary. If `BuildIndex` has not finished, `TotalRows` is a partial count and the filter index would silently miss rows added later. Running both tasks concurrently would require cross-task coordination and complex state management.

**Options**

- **A — Gate `Shift+F`**: disable the key binding until `BuildIndex` is complete.
- **B — Run concurrently**: let `FilterRowIndexer` poll `TotalRows` dynamically and continue scanning as new rows are indexed.

**Decision**: Option A.

**Rationale**

For typical files (up to a few hundred MB), `BuildIndex` completes in well under one second — before a user can interact with `Shift+F`. The guard is a simple boolean check with no ongoing coordination. For very large files where the wait is noticeable, a status indicator ("Indexing…") in the status bar communicates the state to the user. Option B adds significant concurrency complexity for minimal practical benefit.
