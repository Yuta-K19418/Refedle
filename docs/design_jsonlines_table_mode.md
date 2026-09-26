# JSON Lines Table Mode Virtual Viewport - Design Document

## Scope

Implements **Table Mode Virtual Viewport** for JSON Lines format, enabling `.jsonl` files to be
displayed as a tabular grid with virtualized row rendering and dynamic column discovery. This builds
on the existing `SchemaScanner`, `RowIndexer`, and `RowByteCache` infrastructure to provide an
`ITableSource` implementation that bridges raw byte caches to Terminal.Gui's `TableView`.

---

## 1. Requirements

### Functional Requirements

- Display JSON Lines as a table (columns = JSON keys, rows = lines)
- Schema discovery from first 200 lines via existing `SchemaScanner`
- Dynamic column union: new keys discovered during background scanning added as nullable columns
- Missing keys rendered as `<null>`
- On-demand parsing: parse JSON bytes to cell values only when visible in viewport
- Integration with Terminal.Gui `TableView` via `ITableSource` (same pattern as CSV Table Mode)

### Non-Functional Requirements

- **Zero allocation** in parsing hot paths (`Utf8JsonReader` on `ReadOnlySpan<byte>`)
- **Native AOT compatible** (no reflection, no `System.Reflection.Emit`)
- Constant memory regardless of file size (sliding window cache via `RowByteCache`)
- All methods return `Result<T>` for expected failures (no exceptions for control flow)
- Target under 300 lines per class (Single Responsibility Principle)

---

## 2. Architecture

### 2.1 Data Flow

```
File → RowIndexer (existing) → RowByteCache (existing)
                                    |
                        JsonLinesIncrementalSchemaScanner (NEW)
                            → SchemaScanner (existing)
                                    |
                              TableSchema (CoW)
                                    |
                        JsonLinesTableSource (NEW)
                            → CellExtractor (NEW)
                                    |
                        Terminal.Gui TableView (existing)
```

### 2.2 New Components (3 files)

| Component | Layer | File | Responsibility |
|---|---|---|---|
| `CellExtractor` | Engine | `src/Engine/IO/JsonLines/CellExtractor.cs` | Extract cell value from raw JSON bytes by column name |
| `JsonLinesTableSource` | App | `src/App/Tui/Ui/Views/JsonLinesTableSource.cs` | `ITableSource` impl bridging cache + schema + extractor |
| `IncrementalSchemaScanner` | App | `src/App/Tui/Workers/Schema/JsonLines/IncrementalSchemaScanner.cs` | Orchestrate initial + background schema scan |

### 2.3 Modified Files (4 files)

| File | Change |
|---|---|
| `src/App/Tui/ViewMode.cs` | Add `JsonLinesTable` enum value |
| `src/App/Tui/Ui/MainWindow.cs` | Store `JsonLinesIndexer` in `AppState`; add `SwitchToJsonLinesTableView`; add `t` key toggle |
| `src/App/Tui/AppState.cs` | Add `JsonLinesIndexer` and `JsonLinesSchemaScanner` properties |
| `src/App/Schema/IncrementalSchemaScanner.cs` | Move to `src/App/Schema/Csv/IncrementalSchemaScanner.cs` (namespace → `Refedle.App.Schema.Csv`) |

### 2.4 Reused Existing Components

| Component | Path | Usage |
|---|---|---|
| `SchemaScanner` | `src/Engine/IO/JsonLines/SchemaScanner.cs` | `ScanSchema()` and `RefineSchema()` |
| `RowIndexer` | `src/Engine/IO/JsonLines/RowIndexer.cs` | Line offset index for random access |
| `RowByteCache` | `src/Engine/IO/JsonLines/RowByteCache.cs` | Byte-level row retrieval from offsets |
| `RowReader` | `src/Engine/IO/JsonLines/RowReader.cs` | Sequential line reading for schema scan |
| `TableSchema` / `ColumnSchema` | `src/Engine/Models/` | Schema output types |
| `Result<T>` / `Results` | `src/Engine/Result.cs`, `Results.cs` | Error handling |
| `VirtualTableSource` (CSV) | `src/App/Tui/Ui/Views/VirtualTableSource.cs` | Reference pattern for `ITableSource` |
| `IncrementalSchemaScanner` (CSV) | `src/App/Tui/Workers/Schema/Csv/IncrementalSchemaScanner.cs` | Reference pattern for incremental scanning |

---

## 3. Component Design Details

### 3.1 CellExtractor

**File:** `src/Engine/IO/JsonLines/CellExtractor.cs`
**Namespace:** `Refedle.Engine.IO.JsonLines`
**Estimated size:** ~80 lines

Static class with a single public method.

#### API

```csharp
/// <summary>
/// Extracts a cell value from a raw JSON line by column name.
/// Uses Utf8JsonReader for zero-allocation scanning of top-level properties.
/// </summary>
/// <param name="lineBytes">Raw bytes of a single JSON Lines row.</param>
/// <param name="columnNameUtf8">Pre-encoded UTF-8 bytes of the target column name.</param>
/// <returns>String representation of the cell value.</returns>
public static string ExtractCell(
    ReadOnlySpan<byte> lineBytes,
    ReadOnlySpan<byte> columnNameUtf8);
```

#### Value Rendering Rules

| JSON Token | Rendered Output | Notes |
|---|---|---|
| String | Raw string value | Without surrounding quotes |
| Number (integer) | `ToString()` via `GetInt64()` | Fits in Int64 |
| Number (decimal) | `ToString()` via `GetDouble()` | Decimal or overflow |
| `true` / `false` | `"True"` / `"False"` | Boolean display |
| `null` | `"<null>"` | JSON null literal |
| `{ ... }` | `"{...}"` | Collapsed object preview |
| `[ ... ]` | `"[...]"` | Collapsed array preview |
| Column not found | `"<null>"` | Missing key in this row |
| Malformed JSON | `"<error>"` | Parse failure graceful fallback |

#### Key Design Decisions

- **Pre-encoded column names:** `JsonLinesTableSource` stores column names as `byte[][]` to
  avoid repeated `string → UTF-8 byte[]` encoding on every cell access.
- **`reader.ValueTextEquals(columnNameUtf8)`:** Zero-allocation UTF-8 name comparison — avoids
  allocating a string for each property name.
- **Scan all top-level properties:** Linear scan per cell access. This is acceptable because
  JSON Lines rows are typically small (< 1 KB) and only visible rows (~20-50) are parsed.

---

### 3.2 JsonLinesTableSource

**File:** `src/App/Tui/Ui/Views/JsonLinesTableSource.cs`
**Namespace:** `Refedle.App.Tui.Ui.Views`
**Estimated size:** ~80 lines

Implements `Terminal.Gui.Views.ITableSource`, following the same pattern as `VirtualTableSource`
for CSV.

#### API

```csharp
internal sealed class JsonLinesTableSource : ITableSource
{
    /// <param name="cache">Row byte cache for line data retrieval.</param>
    /// <param name="schema">Initial schema from first N lines.</param>
    public JsonLinesTableSource(RowByteCache cache, TableSchema schema);

    public int Rows { get; }       // cache.LineCount
    public int Columns { get; }    // schema.ColumnCount
    public string[] ColumnNames { get; }

    public object this[int row, int col] { get; }  // delegates to CellExtractor

    /// <summary>
    /// Atomically replaces the current schema with a refined version.
    /// Called from background schema scan when new columns are discovered.
    /// Rebuilds pre-encoded column name arrays.
    /// </summary>
    public void UpdateSchema(TableSchema schema);
}
```

#### Internal State

| Field | Type | Purpose |
|---|---|---|
| `_cache` | `RowByteCache` | Raw byte retrieval for each line |
| `_schema` | `volatile TableSchema` | Current schema (atomic replacement) |
| `_columnNames` | `string[]` | Display column names for `TableView` |
| `_columnNameUtf8` | `byte[][]` | Pre-encoded UTF-8 column names for `CellExtractor` |

#### Indexer Implementation

```
this[row, col]:
  1. lineBytes = _cache.GetLineBytes(row)
  2. if lineBytes is empty → return "<null>"
  3. return CellExtractor.ExtractCell(lineBytes, _columnNameUtf8[col])
```

---

### 3.3 IncrementalSchemaScanner (JSON Lines)

**File:** `src/App/Tui/Workers/Schema/JsonLines/IncrementalSchemaScanner.cs`
**Namespace:** `Refedle.App.Tui.Workers.Schema.JsonLines`
**Estimated size:** ~100 lines

Orchestrates initial and background schema scanning, following the same pattern as
`IncrementalSchemaScanner` in `Refedle.App.Tui.Workers.Schema.Csv`.

#### API

```csharp
internal sealed class IncrementalSchemaScanner
{
    private const int InitialScanCount = 200;
    private const int BackgroundBatchSize = 1000;

    /// <param name="filePath">Path to the JSON Lines file.</param>
    public IncrementalSchemaScanner(string filePath);

    /// <summary>
    /// Performs initial scan on first 200 lines.
    /// Reads lines directly via RowReader (independent of RowIndexer to avoid race conditions).
    /// Must be awaited before UI can display schema.
    /// </summary>
    public Task<TableSchema> InitialScanAsync();

    /// <summary>
    /// Starts background scan from line 201 onwards.
    /// Iterates remaining lines in batches of 1000.
    /// Calls SchemaScanner.RefineSchema() per line.
    /// Returns the final refined schema.
    /// </summary>
    public Task<TableSchema> StartBackgroundScanAsync(
        TableSchema currentSchema,
        CancellationToken cancellationToken);
}
```

#### InitialScanAsync Flow

```
1. Task.Run:
   a. Open file via RowReader
   b. Read first 200 lines as ReadOnlyMemory<byte> list
   c. Call SchemaScanner.ScanSchema(lineBytes, InitialScanCount)
   d. If failure → throw InvalidOperationException
   e. Return TableSchema
```

#### StartBackgroundScanAsync Flow

```
1. Task.Run:
   a. Open file via RowReader
   b. Skip first InitialScanCount lines
   c. Read remaining lines in batches of BackgroundBatchSize
   d. For each line: SchemaScanner.RefineSchema(schema, line)
   e. If refined → update schema reference
   f. On cancellation → return current schema
   g. Return final refined schema
```

---

## 4. MainWindow Integration

### 4.1 JSON Lines Load Flow

```
User selects .jsonl file
    → RowIndexer.BuildIndexAsync() (background)
    → AppState.JsonLinesIndexer = indexer
    → SwitchToJsonLinesTreeView(indexer)
```

A JSONL file always opens in Tree mode. Schema scanning does not occur at load time.

### 4.2 Mode Switch Flow (Tree ↔ Table)

Schema scanning is **lazy**: triggered only when the user first switches from Tree to Table mode.

```
[User presses 't' in Tree mode — first switch]
    → new IncrementalSchemaScanner(filePath)
    → IncrementalSchemaScanner.InitialScanAsync() (await)
        → Returns initial TableSchema
    → IncrementalSchemaScanner.StartBackgroundScanAsync() (fire-and-forget)
        → Refined schema → JsonLinesTableSource.UpdateSchema()
    → AppState.JsonLinesSchemaScanner = scanner
    → SwitchToJsonLinesTableView(AppState.JsonLinesIndexer, schema)

[User presses 't' in Table mode]
    → SwitchToJsonLinesTreeView(AppState.JsonLinesIndexer)

[User presses 't' in Tree mode — subsequent switch]
    → AppState.JsonLinesSchemaScanner is not null → reuse cached schema
    → SwitchToJsonLinesTableView(AppState.JsonLinesIndexer, cachedSchema)
```

### 4.3 Mode Switch Keybinding

| Key | From | To |
|---|---|---|
| `t` | `JsonLinesTree` | `JsonLinesTable` |
| `t` | `JsonLinesTable` | `JsonLinesTree` |

The toggle shortcut is shown in the status bar while a JSONL file is loaded:

```
^O Open  |  t Toggle Table  |  ^X Quit
```

### 4.4 ViewMode Addition

```csharp
/// <summary>
/// JSON Lines table view with virtualized grid rendering.
/// </summary>
JsonLinesTable,
```

### 4.5 AppState Additions

```csharp
/// <summary>
/// Gets or sets the JSON Lines row indexer for the current file.
/// Stored on load so it can be reused when switching between Tree and Table modes.
/// </summary>
public Engine.IO.JsonLines.RowIndexer? JsonLinesIndexer { get; set; }

/// <summary>
/// Gets or sets the JSON Lines schema scanner for the current file.
/// Null until the user switches to Table mode for the first time (lazy initialization).
/// </summary>
public JsonLines.IncrementalSchemaScanner? JsonLinesSchemaScanner { get; set; }
```

---

## 5. Thread Safety

| Component | Safety | Mechanism |
|---|---|---|
| `CellExtractor` | Thread-safe | Stateless static method, operates on `ReadOnlySpan<byte>` |
| `JsonLinesTableSource._schema` | Thread-safe update | `volatile` field, atomic reference replacement |
| `JsonLinesTableSource._columnNameUtf8` | Thread-safe update | Rebuilt atomically alongside schema |
| `RowByteCache` | UI thread only | Single-thread access (Terminal.Gui event loop) |
| Background schema scan | Thread pool | Communicates via Copy-on-Write schema replacement |

### Schema Update Safety

The background scan thread updates the schema via `JsonLinesTableSource.UpdateSchema()`:

1. New `TableSchema` is built (immutable, Copy-on-Write)
2. New `byte[][]` column name array is built
3. Both are assigned atomically via `volatile` field write
4. UI thread reads the latest schema on next cell access

This avoids locks entirely — the UI thread always sees a consistent snapshot.

---

## 6. Dynamic Column Union

### Flow

1. **Initial scan (200 lines):** `SchemaScanner.ScanSchema()` produces base `TableSchema`
2. **Background scan (remaining lines):** `SchemaScanner.RefineSchema()` per line
3. **New column discovered:** Added with `IsNullable = true` (absent in prior rows)
4. **Schema updated:** `JsonLinesTableSource.UpdateSchema()` called with refined schema
5. **Column names rebuilt:** Pre-encoded `byte[][]` array reconstructed
6. **UI renders:** `CellExtractor.ExtractCell()` returns `"<null>"` for rows missing the new column

### Example

```
Line 1: {"name": "Alice", "age": 30}
Line 2: {"name": "Bob", "age": 25, "email": "bob@example.com"}
```

After initial scan:
- Columns: `name (Text)`, `age (WholeNumber)`, `email (Text, nullable)`
- Line 1 cell for `email`: `"<null>"` (key absent)

---

## 7. Test Plan

### 7.1 CellExtractorTests

| Scenario | Expected |
|---|---|
| Extract string value | Returns unquoted string |
| Extract integer value | Returns number as string |
| Extract decimal value | Returns number as string |
| Extract boolean value | Returns `"True"` / `"False"` |
| Extract null value | Returns `"<null>"` |
| Extract nested object | Returns `"{...}"` |
| Extract array | Returns `"[...]"` |
| Missing key | Returns `"<null>"` |
| Malformed JSON line | Returns `"<error>"` |
| Empty line | Returns `"<error>"` |

### 7.2 JsonLinesTableSourceTests

| Scenario | Expected |
|---|---|
| Cell retrieval for existing key | Correct value via `CellExtractor` |
| Cell retrieval for missing key | `"<null>"` |
| Schema update adds new column | `Columns` count increases, `ColumnNames` updated |
| Row/col out of bounds | `ArgumentOutOfRangeException` |
| `Rows` reflects cache line count | Matches `RowByteCache.LineCount` |

### 7.3 JsonLinesIncrementalSchemaScannerTests

| Scenario | Expected |
|---|---|
| Initial scan with valid file | Returns `TableSchema` with correct columns |
| Initial scan with empty file | Throws `InvalidOperationException` |
| Background scan discovers new column | Refined schema includes new nullable column |
| Background scan with cancellation | Returns current schema, no exception |
| Background scan with no remaining lines | Returns original schema unchanged |

### 7.4 BenchmarkDotNet

- Cell extraction performance on representative JSON Lines data
- Compare single-column extraction vs full-row extraction
- Memory allocation verification (target: zero per cell access)

---

## 8. Edge Cases

| Scenario | Handling |
|---|---|
| Empty `.jsonl` file | `InitialScanAsync` fails → error displayed to user |
| All lines malformed | `ScanSchema` returns `Failure` → error displayed |
| Very wide rows (many keys) | Column name array grows; no performance issue (scanned once) |
| Very deep nesting | `CellExtractor` only scans top-level; nested shown as `"{...}"` |
| Extremely long string values | Rendered as-is; `TableView` handles truncation |
| Unicode column names | `Utf8JsonReader` handles natively; pre-encoded as UTF-8 bytes |
| Duplicate keys in one line | Last value wins (`Utf8JsonReader` default behavior) |
| Mixed: some lines objects, some arrays | Non-object lines skipped by `SchemaScanner` |

---

## 9. Files to Create / Modify

| File | Action | Purpose |
|---|---|---|
| `src/Engine/IO/JsonLines/CellExtractor.cs` | **Create** | Cell value extraction from raw JSON bytes |
| `src/App/Tui/Ui/Views/JsonLinesTableSource.cs` | **Create** | `ITableSource` implementation for JSON Lines |
| `src/App/Tui/Workers/Schema/JsonLines/IncrementalSchemaScanner.cs` | **Create** | Incremental schema scanning orchestration |
| `src/App/Schema/Csv/IncrementalSchemaScanner.cs` | Move (rename namespace) | Existing CSV schema scanner; namespace → `Refedle.App.Schema.Csv` |
| `src/App/Tui/ViewMode.cs` | Modify | Add `JsonLinesTable` enum value |
| `src/App/Tui/Ui/MainWindow.cs` | Modify | Store `JsonLinesIndexer` in `AppState`; add `SwitchToJsonLinesTableView`; add `t` key toggle |
| `src/App/Tui/AppState.cs` | Modify | Add `JsonLinesIndexer` and `JsonLinesSchemaScanner` properties |
| `tests/Refedle.Tests/Engine/IO/JsonLines/CellExtractorTests.cs` | **Create** | Cell extraction tests |
| `tests/Refedle.Tests/App/Tui/Ui/Views/JsonLinesTableSourceTests.cs` | **Create** | Table source tests |
| `tests/Refedle.Tests/App/Tui/Workers/Schema/JsonLines/IncrementalSchemaScannerTests.cs` | **Create** | Schema scanner tests |
| `docs/design_jsonlines_table_mode.md` | **Create** | This document |

---

## 10. Acceptance Criteria

Maps to Issue #58 acceptance criteria:

- [ ] Displays JSON Lines as table (`JsonLinesTableSource` + `TableView`)
- [ ] Schema discovered from first N lines (`IncrementalSchemaScanner.InitialScanAsync`, triggered on first Tree → Table switch)
- [ ] New columns added dynamically during scrolling (background `RefineSchema` + `UpdateSchema`)
- [ ] Missing keys show as `<null>` (`CellExtractor` returns `"<null>"`)
- [ ] Zero allocations for visible row rendering (`Utf8JsonReader` on `ReadOnlySpan<byte>`)
- [ ] Integration with `TableView` (`ITableSource` implementation)
- [ ] Unit tests for schema discovery logic (`CellExtractorTests`, `JsonLinesTableSourceTests`, etc.)
- [ ] BenchmarkDotNet tests for rendering performance
