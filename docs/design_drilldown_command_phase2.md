# DrillDown Command — Phase 2 Design Document

## Scope

Implements **DrillDown Phase 2: Full Aggregation** — a file-wide scan that collects all child
objects at the selected key path across every record in the file and presents them as a single
flat table.

| Phase | Scope | Description |
|-------|-------|-------------|
| **Phase 1** (complete) | Selected node only | Displays children of the selected `JsonArrayTreeNode` using in-memory node bytes. |
| **Phase 2** (this document) | Full file scan | Scans the entire file for all records containing the selected key path. Aggregates their child objects into one table. |

**Supported formats for Phase 2:** JSON Lines, JSON Array only.
JSON Object is excluded — a single document has no records to aggregate across.

**Out of scope (deferred):**
- Back / Undo navigation (Backspace to return to Tree)
- Breadcrumb path display
- Progress indication during long file scans
- Memory-efficient viewport-based row loading: all matched rows are materialized in memory
  after the scan. For files with millions of records this may consume significant memory.
  Deferred to a future phase.
- Incremental schema/row updates during the scan: the table is currently updated only once,
  after the entire scan completes, which does not scale well for very large files (the UI shows
  no progress until the scan is fully done). Deferred — to be optimized in a future phase.

---

## 1. Requirements

### 1.1 Enable Conditions

| Format | DrillDown Behavior |
|--------|--------------------|
| JSON Lines | `x` key on any tree node → always full file scan (Phase 2) |
| JSON Array | `x` key on any tree node → always full file scan (Phase 2) |
| JSON Object | Phase 1 behavior retained: `x` key on `JsonArrayTreeNode` → single-node DrillDown |

Any node type may trigger DrillDown: `JsonObjectTreeNode`, `JsonArrayTreeNode`, or primitive leaf
nodes. The selected node's type determines the output format (see Section 1.4).

### 1.2 Key Path Construction

When "DrillDown" is triggered, the `ParentNode` chain of the selected node is traversed upward
to build a `KeyPath`: an ordered list of path segments from root to the selected node.

Segment contribution rules:

| Node type | Segment added |
|-----------|---------------|
| `JsonObjectTreeNode` with `KeyName = "orders"` | `"orders"` |
| `JsonArrayTreeNode` with `KeyName = "tags"` | `"tags"` |
| Array-index display node (e.g. `[0]`) | `"[0]"` (literal string; index value retained as array marker) |
| Root sentinel node (no KeyName) | *(no segment)* |

The path is built lazily at trigger time only. Each tree node carries a single
`ParentNode { get; init; }` reference (8-byte pointer set at construction). No list object is
allocated per node at construction time.

Example: selected node `orders[0].shipping` → `KeyPath = ["orders", "[0]", "shipping"]`

### 1.3 Path Traversal Rules

The scanner follows `KeyPath` segment by segment for each record in the file:

| Segment type | Identification | Action |
|---|---|---|
| Key segment (e.g. `"orders"`, `"shipping"`) | Does **not** start with `[` | Navigate into the JSON object by this key. If key absent or value is wrong type → skip record silently |
| Index segment (e.g. `"[0]"`) | Starts with `[` | Ignore the numeric index. **Expand all array elements** and continue traversal independently for each |

**Core rule:** `[n]` signals that a JSON array was present at this path position.
The index value is discarded — all elements are always expanded.
Therefore `orders` and `orders[0]` always produce identical DrillDown output.

If at any point the expected JSON structure is not found (key absent, wrong token type for the
next segment), that record is **silently skipped** — no error is raised.

### 1.4 Output Format

After the full `KeyPath` is followed, the leaf value determines the output. Long format is used
in all cases: each scalar value occupies exactly one row.

| Leaf type | Columns | Output |
|---|---|---|
| JSONObject | Each key of the object becomes a column | One row per record (or per parent array element) |
| JSONArray of JSONObject | Each key of child objects becomes a column | One row per child element; `#` gains one level |
| JSONArray of Primitive | Single `value` column | One row per element; `#` gains one level |
| Primitive | Single column named after the key | One row per record (or per parent array element) |

Examples below use JSON Lines. The output structure is the same for JSON Array; only the `#` value differs — see Section 1.5.

#### JSONObject

```json
// Record pos=1
{ "user": { "name": "Alice", "age": 30 } }
// Record pos=2
{ "user": { "name": "Bob", "age": 25 } }
```

**`user`** (0 arrays in path):
```
#  | name  | age
1  | Alice | 30
2  | Bob   | 25
```

#### JSONArray of JSONObject

```json
// Record pos=1
{ "orders": [{ "id": "A1", "qty": 2 }, { "id": "A2", "qty": 5 }] }
// Record pos=2
{ "orders": [{ "id": "B1", "qty": 1 }] }
```

**`orders` / `orders[0]`** (same output — 1 array in path):
```
#   | id | qty
1:0 | A1 | 2
1:1 | A2 | 5
2:0 | B1 | 1
```

#### JSONArray of Primitive

```json
// Record pos=1
{ "tags": ["dev", "ops"] }
// Record pos=2
{ "tags": ["dev"] }
```

**`tags` / `tags[0]`** (same output — 1 array in path):
```
#   | value
1:0 | dev
1:1 | ops
2:0 | dev
```

When the path contains multiple `[n]` segments, the `#` column gains additional levels:

```json
// Record pos=1
{ "orders": [{ "tags": ["urgent", "gift"] }, { "tags": ["normal"] }] }
```

**`orders[0].tags`** (2 arrays in path — `[0]` + leaf):
```
#     | value
1:0:0 | urgent
1:0:1 | gift
1:1:0 | normal
```

#### Primitive

```json
// Record pos=1
{ "score": 88 }
// Record pos=2
{ "score": 72 }
```

**`score`** (0 arrays in path):
```
#  | score
1  | 88
2  | 72
```

#### Nested Values

When a column value is itself a JSON object or array, it is rendered as `{...}` or `[...]` —
display is 1 level deep only.

```json
// Record pos=1
{ "user": { "name": "Alice", "address": { "city": "Tokyo" }, "tags": ["dev", "ops"] } }
```

**`user`**:
```
#  | name  | address | tags
1  | Alice | {...}   | [...]
```

### 1.5 `#` Column

The number of `:` separators in `#` equals the number of arrays traversed along the path
(including the leaf node if the leaf itself is a JSONArray).

| Selected path | Arrays traversed | `#` format |
|---|---|---|
| `score` | 0 | `pos` |
| `user` | 0 | `pos` |
| `orders` | 1 (leaf array) | `pos:i` |
| `orders[0]` | 1 (`[0]` segment) | `pos:i` |
| `orders[0].shipping` | 1 (`[0]` segment) | `pos:i` |
| `orders[0].tags` | 2 (`[0]` + leaf array) | `pos:i:j` |

Record position numbering:

| Format | Position basis | Example `#` |
|---|---|---|
| JSON Lines | 1-based line number | `3:0`, `3:1` |
| JSON Array | 0-based element index | `2:0`, `2:1` |

### 1.6 Null Handling

| Situation | Output |
|---|---|
| `KeyPath` followed successfully; leaf value is JSON `null` | Null row is output (one row with null cell values) |
| A `KeyPath` segment is absent in the record | Record is silently skipped |

### 1.7 Error Cases

| Condition | Error dialog message | When it occurs |
|---|---|---|
| No records produce any rows after full scan | `"No matching records found."` | All records have an empty array `[]` at the target path |
| Rows were collected but all collected objects have no keys | `"All child objects have no keys"` | All records have an array of empty objects (e.g. `[{}, {}]`) at the target path — rows are collected but the schema has zero columns |

### 1.8 Non-Functional

- Zero allocation in cell extraction hot paths (`Utf8JsonReader` on `ReadOnlySpan<byte>`)
- Native AOT compatible (no reflection)
- All classes stay under 300 lines
- 1-pass file scan; schema and rows are accumulated in memory as new columns are discovered
  during the scan, then applied to the UI in a single `_uiThreadInvoke` call after the scan
  completes (no second scan required)
- Cancellable via `CancellationToken`

---

## 2. Architecture

### 2.1 Motivation: FocusedTableSource Refactor

**Current state (Phase 1)**

`FocusedTableSource` renders the `#` column via `FormatHashColumn(row)`, which returns
`"{_recordPosition}:{row}"` using a single `long? _recordPosition` shared across all rows.
This works because Phase 1 DrillDown operates on a single `JsonArrayTreeNode` — all child rows
come from the same record, so `_recordPosition` is constant and `row` serves as both the
table row index and the array element index within that record.

**Problems Phase 2 introduces**

1. **`row` no longer equals the array element index** — Phase 2 aggregates rows from many
   records. The table row index (`row`) increments continuously across all records, but the
   array element index resets to 0 for each new record. `FormatHashColumn(row)` therefore
   produces the wrong element index for any record after the first.

2. **Primitive leaves** — Phase 1 assumed all rows are JSON objects. Phase 2 also encounters
   primitive leaves, requiring branching on whether the leaf value is a primitive or an object.
   See Section 3.7 for details.

**Solution: `FocusedTableRow`**

Each row pre-computes its own `#` string at scan time — when the correct element index is
known — and carries its own bytes. `FocusedTableSource` reads `_rows[row].HashValue` for the
`#` column and `_rows[row].Bytes` for cell extraction — no per-table format logic needed.

`DrillDownState` drops the `ChildRawValues / Format / RecordPosition` triple and stores
`IReadOnlyList<FocusedTableRow>` instead.

### 2.2 Data Flow

```
[User presses x on any tree node in MorphTreeView → ActionMenu shows "DrillDown"]

── JSON Object (Phase 1, retained) ─────────────────────────────────────────────────────────
  Condition: selected node is JsonArrayTreeNode; all children are JsonObjectTreeNode
  AppKeyHandler → ActionMenuDialog(["DrillDown"])
                → ViewManager.DrillDown(request)
                → ModeController.DrillDown(request)
                      DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, format)
                      build FocusedTableRow[] inline (hash = $"[{i}]")
                      → DrillDownState { Rows, Schema }
                → SwitchToFocusedTable(drillDown)

── JSON Lines / JSON Array (Phase 2, new) ────────────────────────────────────────────────
  Condition: any node selected (no type restriction)
  AppKeyHandler → BuildKeyPath(selectedNode) → keyPath
                → ActionMenuDialog(["DrillDown"])
                → ViewManager.FullAggregationDrillDownAsync(request)
                → ModeController.FullAggregationDrillDownAsync(request)
                      await Task.Run(() =>
                          FullAggregationScanner.Scan(filePath, format, keyPath, ct))
                      → Result<DrillDownState>
                → on UI thread (_uiThreadInvoke):
                      _state.DrillDown = drillDown
                      _state.CurrentMode = ViewMode.FocusedTable
                      SwitchToFocusedTable(drillDown)
```

### 2.3 New Components

| Component | Layer | File | Responsibility |
|-----------|-------|------|----------------|
| `FocusedTableRow` | Engine | `src/Engine/IO/DrillDown/FocusedTableRow.cs` | Value type pairing child bytes with pre-computed `#` value |
| `SchemaScanner` | Engine | `src/Engine/IO/DrillDown/SchemaScanner.cs` | Stateless static helpers for accumulating key order / column types across multiple JSON objects and building the final `TableSchema`; shared by `DrillDownSchemaExtractor` and `FullAggregationScanner` |
| `FullAggregationScanner` | Engine | `src/Engine/IO/DrillDown/FullAggregationScanner.cs` | Full file scan: traverse KeyPath for each record; collect rows for all leaf types; build union schema |

### 2.4 Modified Files

| File | Change |
|------|--------|
| `src/Engine/IO/DrillDown/DrillDownSchemaExtractor.cs` | `BuildSchema` delegates to new `SchemaScanner` static helpers instead of owning them; `ScanObject`/`RegisterKeyIfNew`/`IncrementObservationCounts`/`MergeColumnType` move out |
| `src/App/Tui/DrillDownState.cs` | Replace `ChildRawValues / Format / RecordPosition` with `IReadOnlyList<FocusedTableRow> Rows` |
| `src/App/Tui/Ui/Views/FocusedTableSource.cs` | Refactor: accept `DrillDownState` with new `Rows`; replace `FormatHashColumn` with `_rows[row].HashValue` |
| `src/App/Tui/DrillDownRequest.cs` | Split the single record into an abstract `DrillDownRequest` base plus `SingleDrillDownRequest` and `FullAggregationDrillDownRequest` |
| `src/App/Tui/Ui/Views/JsonObjectTreeNode.cs` | Add `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonArrayTreeNode.cs` | Add `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonValueTreeNode.cs` | Add `public string? KeyName { get; init; }` and `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonTreeNodeHelper.cs` | Add `ITreeNode? parentNode` parameter to `CreateChildNode`, `CreateNestedObjectNode`, `CreateNestedArrayNode` |
| `src/App/Tui/Ui/ModeController.cs` | Update `DrillDown()` to build rows inline; add `FullAggregationDrillDownAsync()` returning `Result<DrillDownState>` |
| `src/App/Tui/Ui/ViewManager.cs` | Add `FullAggregationDrillDownAsync()`; assign `_state.DrillDown` / `_state.CurrentMode` inside `_uiThreadInvoke` |
| `src/App/Tui/Ui/AppKeyHandler.cs` | Single "DrillDown" option for all formats; add `BuildKeyPath`; dispatch Phase 1 vs Phase 2 by format |
| `tests/.../FocusedTableSourceTests.cs` | Update for new constructor shape |
| `tests/.../FullAggregationScannerTests.cs` | New: scanner unit tests |

---

## 3. Component Design

### 3.1 FocusedTableRow (new)

**File:** `src/Engine/IO/DrillDown/FocusedTableRow.cs`
**Namespace:** `Refedle.Engine.IO.DrillDown`
**Estimated size:** ~5 lines

```csharp
/// <summary>A single row in the FocusedTable: child object bytes and its pre-computed # value.</summary>
public readonly record struct FocusedTableRow(JsonRawBytes Bytes, string HashValue);
```

`public` visibility: referenced by App layer (`FocusedTableSource`, `ModeController`).

`struct` is chosen over `class` as a minor optimization: when stored in `IReadOnlyList<FocusedTableRow>`, elements are laid out inline in the backing array, avoiding per-element object header allocation and one level of indirection. The benefit is marginal at typical row counts (hundreds to thousands) because the actual data (`byte[]` behind `JsonRawBytes` and `string`) still lives on the heap. A `readonly record` (class) would be equally valid.

`Bytes` always contains valid JSON object bytes. For object and array-of-object leaves this is the
raw child bytes from the file. For primitive leaves and array-of-primitive leaves, `Bytes` holds a
synthetic object constructed by `FullAggregationScanner` (see Section 3.7).

### 3.2 DrillDownState (modified)

**File:** `src/App/Tui/DrillDownState.cs`

Replace the three-field constructor with:

```csharp
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema);
```

`Format` and `RecordPosition` are removed — each `FocusedTableRow.HashValue` already holds the
final `#` string, making them redundant.

### 3.3 FocusedTableSource (refactored)

**File:** `src/App/Tui/Ui/Views/FocusedTableSource.cs`

Constructor accepts `DrillDownState` (unchanged call site).
Internal state changes:

| Old field | New field | Change |
|-----------|-----------|--------|
| `_childRawValues` | `_rows` (`IReadOnlyList<FocusedTableRow>`) | Replaces raw bytes list |
| `_recordPosition` | *(removed)* | Hash pre-computed per row |

```
Rows    = _rows.Count                                            (unchanged)
Columns = _schema.ColumnCount + 1                               (unchanged)
this[row, 0] → _rows[row].HashValue                            (replaces FormatHashColumn)
this[row, n] → JsonObjectCellExtractor.ExtractCell(
                   _rows[row].Bytes.Span,
                   _columnNamesUtf8[n - 1])                    (unchanged)
```

`FormatHashColumn()` is removed. Because all rows carry synthesized or real JSON objects in
`Bytes`, `JsonObjectCellExtractor` works without modification.

### 3.4 DrillDownRequest (modified)

**File:** `src/App/Tui/DrillDownRequest.cs`

Replace the single `DrillDownRequest` with a base type and two dedicated subtypes,
aligned with the already-split method signatures in `ViewManager` and `ModeController`.

```csharp
internal abstract record DrillDownRequest(DataFormat Format);

/// <summary>Single-node DrillDown: JSON Object format, operates on the selected node only.</summary>
internal sealed record SingleDrillDownRequest(
    DataFormat Format,
    JsonRawBytes NodeBytes)
    : DrillDownRequest(Format);

/// <summary>Full-aggregation DrillDown: JSON Lines / JSON Array format, scans the entire file.</summary>
internal sealed record FullAggregationDrillDownRequest(
    DataFormat Format,
    IReadOnlyList<string> KeyPath)
    : DrillDownRequest(Format);
```

`KeyPath` is the ordered list of path segments from root to the selected node, built by
`AppKeyHandler.BuildKeyPath` at trigger time.

### 3.5 JsonObjectTreeNode / JsonArrayTreeNode / JsonValueTreeNode (modified)

**Files:** `src/App/Tui/Ui/Views/JsonObjectTreeNode.cs`, `src/App/Tui/Ui/Views/JsonArrayTreeNode.cs`,
`src/App/Tui/Ui/Views/JsonValueTreeNode.cs`

Add `ParentNode` to `JsonObjectTreeNode` and `JsonArrayTreeNode`:

```csharp
public ITreeNode? ParentNode { get; init; }
```

`JsonValueTreeNode` represents primitive leaf nodes. It currently has no `KeyName` or `ParentNode`.
Add both properties so `BuildKeyPath` can traverse through it:

```csharp
// Add to JsonValueTreeNode
public string? KeyName { get; init; }
public ITreeNode? ParentNode { get; init; }
```

The `KeyName` for a `JsonValueTreeNode` is the label already passed at construction time
(the property name, e.g., `"score"`).

In `JsonTreeNodeHelper.CreateChildNode` (and its variants `CreateNestedObjectNode`,
`CreateNestedArrayNode`), add a `ITreeNode? parentNode` parameter and pass it through when
constructing each node type:

```csharp
new JsonObjectTreeNode(...)
{
    KeyName    = label,
    ParentNode = parentNode,
}
new JsonValueTreeNode(...)
{
    KeyName    = label,
    ParentNode = parentNode,
}
```

Root nodes have `ParentNode = null`.

**Memory cost:** one 8-byte pointer per node. No list object is allocated per node at
construction time.

### 3.6 SchemaScanner (new)

**File:** `src/Engine/IO/DrillDown/SchemaScanner.cs`
**Namespace:** `Refedle.Engine.IO.DrillDown`

Stateless static class holding the schema-accumulation logic previously private to
`DrillDownSchemaExtractor`. Both `DrillDownSchemaExtractor.BuildSchema` (Phase 1) and
`FullAggregationScanner` (Phase 2) call these methods directly and own their own accumulator
collections (`keyOrder`, `keySet`, `columnTypes`, `keyObservedCount`) as local variables —
`SchemaScanner` holds no state of its own. Signatures are unchanged from the current
`DrillDownSchemaExtractor` implementation; only the owning class changes.

```csharp
internal static class SchemaScanner
{
    public static void ScanObject(
        ReadOnlySpan<byte> objectBytes,
        List<string> keyOrder,
        HashSet<string> keySet,
        Dictionary<string, ColumnType> columnTypes,
        HashSet<string> observedKeys);

    public static void RegisterKeyIfNew(
        string key,
        List<string> keyOrder,
        HashSet<string> keySet);

    public static void IncrementObservationCounts(
        HashSet<string> observedKeys,
        Dictionary<string, int> keyObservedCount);

    public static TableSchema BuildTableSchema(
        List<string> keyOrder,
        Dictionary<string, ColumnType> columnTypes,
        Dictionary<string, int> keyObservedCount,
        int totalRowCount,    // pass rows.Count after the scan loop completes
        DataFormat format);
}
```

`ScanSingleProperty` and `MergeColumnType` move here too, as `private` implementation details of
`ScanObject`.

**Why a separate class instead of exposing `DrillDownSchemaExtractor` internals:** making these
methods `internal` on `DrillDownSchemaExtractor` would make `FullAggregationScanner` (a Phase 2
component) depend on a Phase 1 component's implementation details. With `SchemaScanner` as a
sibling that both depend on, neither scanner depends on the other.

**`DrillDownSchemaExtractor.BuildSchema`** (Phase 1) becomes a thin caller with no behavior
change from the current implementation:

```csharp
private static Result<TableSchema> BuildSchema(List<JsonRawBytes> childRawValues, DataFormat format)
{
    List<string> keyOrder = [];
    var keySet = new HashSet<string>(StringComparer.Ordinal);
    var columnTypes = new Dictionary<string, ColumnType>(StringComparer.Ordinal);
    var keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal);

    foreach (var childBytes in childRawValues)
    {
        var observedKeys = new HashSet<string>(StringComparer.Ordinal);
        SchemaScanner.ScanObject(childBytes.Span, keyOrder, keySet, columnTypes, observedKeys);
        SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount);
    }

    if (keyOrder.Count == 0)
        return Results.Failure<TableSchema>("All child objects have no keys");

    return Results.Success(SchemaScanner.BuildTableSchema(
        keyOrder, columnTypes, keyObservedCount, childRawValues.Count, format));
}
```

### 3.7 FullAggregationScanner (new)

**File:** `src/Engine/IO/DrillDown/FullAggregationScanner.cs`
**Namespace:** `Refedle.Engine.IO.DrillDown`
**Estimated size:** ~250 lines

`public` visibility: called by `ModeController` (App layer) cross-assembly.

#### API

```csharp
public static class FullAggregationScanner
{
    /// <summary>
    /// Scans <paramref name="filePath"/> for all records and traverses <paramref name="keyPath"/>
    /// to collect leaf values of every matching path. Supports all leaf types: JSON Object,
    /// JSON Array of Object, JSON Array of Primitive, and scalar Primitive.
    /// Returns <c>Failure</c> when format is JSON Object, no rows are collected, or
    /// all collected leaf objects have no keys.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)> Scan(
        string filePath,
        DataFormat format,
        IReadOnlyList<string> keyPath,
        CancellationToken cancellationToken = default);
}
```

#### Top-Level Algorithm

`colName` / `colNameUtf8` are derived from `keyPath` and never change during a scan.
They are computed once here and threaded through as parameters to avoid per-record allocation.

```
if format == JsonObject → return Failure("JSON Object format does not support full aggregation.")

Open file with MmapService
List<FocusedTableRow> rows     = []
List<string>          keyOrder = []
keySet           = new HashSet<string>(StringComparer.Ordinal)
columnTypes      = new Dictionary<string, ColumnType>(StringComparer.Ordinal)
keyObservedCount = new Dictionary<string, int>(StringComparer.Ordinal)

colName     = LastKeySegment(keyPath)           // pre-computed once
colNameUtf8 = Encoding.UTF8.GetBytes(colName)  // pre-computed once

dispatch:
    format == JsonLines → ScanLines(mmap, keyPath, colName, colNameUtf8,
                                    rows, keyOrder, keySet, columnTypes,
                                    keyObservedCount, cancellationToken)
    format == JsonArray → ScanElements(mmap, keyPath, colName, colNameUtf8,
                                       rows, keyOrder, keySet, columnTypes,
                                       keyObservedCount, cancellationToken)

if rows.Count == 0 → return Failure("No matching records found.")
if keyOrder.Count == 0 → return Failure("All child objects have no keys")

schema = SchemaScanner.BuildTableSchema(
    keyOrder, columnTypes, keyObservedCount, rows.Count, format)
return Success((schema, rows))
```

#### ScanLines (JSON Lines)

Newline detection mirrors `RowReader.FindNextLineLength` — UTF-8 BOM detection, `Array.MaxLength`
guard, and trailing-line-without-newline handling.

```
recordPosition = 1L   (1-based)
scan line by line:
    for each line bytes (trimmed of \r\n):
        cancellationToken.ThrowIfCancellationRequested()
        if line is empty → recordPosition++; continue
        TryExtractRows(lineBytes, keyPath, recordPosition.ToString(),
                       colName, colNameUtf8, rows,
                       keyOrder, keySet, columnTypes, keyObservedCount)
        recordPosition++
```

#### ScanElements (JSON Array)

```
recordPosition = 0L   (0-based)
using Utf8JsonReader on mmap span:
    expect StartArray token at root depth
    for each element at depth 1:
        cancellationToken.ThrowIfCancellationRequested()
        elementBytes = extract element bytes
        TryExtractRows(elementBytes, keyPath, recordPosition.ToString(),
                       colName, colNameUtf8, rows,
                       keyOrder, keySet, columnTypes, keyObservedCount)
        recordPosition++
```

#### TryExtractRows

```
input: recordBytes, keyPath, posHash (string), colName, colNameUtf8, rows,
       keyOrder, keySet, columnTypes, keyObservedCount

TraverseKeyPath(recordBytes, keyPath, segmentIndex: 0, posHash,
                colName, colNameUtf8, rows,
                keyOrder, keySet, columnTypes, keyObservedCount)
```

#### TraverseKeyPath (recursive; depth bounded by keyPath.Count)

```
input: currentBytes, keyPath, segIdx, posHash,
       colName, colNameUtf8, rows, keyOrder, keySet, columnTypes, keyObservedCount

if segIdx == keyPath.Count:
    CollectLeafRows(currentBytes, posHash, colName, colNameUtf8,
                    rows, keyOrder, keySet, columnTypes, keyObservedCount)
    return

segment = keyPath[segIdx]

if segment starts with '[':
    // Index segment → expand all array elements
    if first token of currentBytes != StartArray → return  // wrong type, skip silently

    if segIdx == keyPath.Count - 1:
        // Trailing index segment: "tags" and "tags[0]" must produce identical output
        // (including the "value" column for primitive elements), so delegate directly
        // to the same array-leaf collection used when the array itself is the leaf.
        CollectArrayLeafRows(currentBytes, posHash, rows, keyOrder, keySet, columnTypes, keyObservedCount)
        return

    elementIdx = 0
    for each element bytes in currentBytes (via Utf8JsonReader):
        TraverseKeyPath(elementBytes, keyPath, segIdx + 1, $"{posHash}:{elementIdx}",
                        colName, colNameUtf8, rows,
                        keyOrder, keySet, columnTypes, keyObservedCount)
        elementIdx++
else:
    // Key segment → navigate into object
    if first token of currentBytes != StartObject → return  // wrong type, skip silently
    valueBytes = FindValueByKey(currentBytes, segment)
    if valueBytes is null → return  // key absent, skip silently
    TraverseKeyPath(valueBytes, keyPath, segIdx + 1, posHash,
                    colName, colNameUtf8, rows,
                    keyOrder, keySet, columnTypes, keyObservedCount)
```

#### CollectLeafRows

Determines row(s) to emit based on the token type at the leaf position reached after
full KeyPath traversal.

```
input: leafBytes, posHash, colName, colNameUtf8, rows,
       keyOrder, keySet, columnTypes, keyObservedCount

tokenType = first JSON token of leafBytes

case StartObject:
    // JSONObject leaf — one row per record (or per array element from TraverseKeyPath)
    rows.Add(new FocusedTableRow(leafBytes, posHash))
    observedKeys = new HashSet<string>(StringComparer.Ordinal)
    SchemaScanner.ScanObject(leafBytes.Span, keyOrder, keySet,
                              columnTypes, observedKeys)
    SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount)

case StartArray:
    // JSONArray leaf — expand each element; # gains one level
    elementIdx = 0
    for each element in leafBytes (via Utf8JsonReader):
        elHash = $"{posHash}:{elementIdx}"
        if element is StartObject:
            rows.Add(new FocusedTableRow(elementBytes, elHash))
            observedKeys = new HashSet<string>(StringComparer.Ordinal)
            SchemaScanner.ScanObject(elementBytes.Span, keyOrder, keySet,
                                      columnTypes, observedKeys)
            SchemaScanner.IncrementObservationCounts(observedKeys, keyObservedCount)
        else:
            // Primitive element → synthesize {"value": <element>}
            synthBytes = SynthesizeObject("value"u8, elementBytes.Span)
            rows.Add(new FocusedTableRow(synthBytes, elHash))
            SchemaScanner.RegisterKeyIfNew("value", keyOrder, keySet)
            var valueObserved = new HashSet<string>(StringComparer.Ordinal) { "value" }
            SchemaScanner.IncrementObservationCounts(valueObserved, keyObservedCount)
        elementIdx++

default (Primitive):
    // Primitive leaf → synthesize {<colName>: <leaf>}
    // colName and colNameUtf8 are pre-computed from LastKeySegment(keyPath)
    synthBytes = SynthesizeObject(colNameUtf8, leafBytes.Span)
    rows.Add(new FocusedTableRow(synthBytes, posHash))
    SchemaScanner.RegisterKeyIfNew(colName, keyOrder, keySet)
    var colObserved = new HashSet<string>(StringComparer.Ordinal) { colName }
    SchemaScanner.IncrementObservationCounts(colObserved, keyObservedCount)
    // Note: ScanObject is NOT called here, so no type inference is performed.
    // The synthesized column will receive ColumnType.Text (BuildTableSchema default).
    // This is a known Phase 2 limitation — numeric primitive leaves are not typed as Number.
```

> **Phase 2 Limitation:** Synthesized primitive columns (`"value"` for array-of-primitive leaves,
> and the key-named column for scalar primitive leaves) always receive `ColumnType.Text`.
> `ScanObject` is not called on synthesized objects, so `TypeInferrer` never runs for these paths.
> As a result, a numeric field such as `{"score": 88}` will be typed as `Text`, not `Number`.
> Typed inference for synthesized primitives is deferred to a future phase.

#### FindValueByKey (private)

Parses `objectBytes` as a JSON object with `Utf8JsonReader`, returns
`JsonByteExtractor.ExtractNestedBytes` for the value when `PropertyName == key`; returns `null`
if the key is absent.

#### SynthesizeObject (private)

Creates a synthetic single-key JSON object for primitive rows so that `JsonObjectCellExtractor`
can extract cell values without modification:

```csharp
private static JsonRawBytes SynthesizeObject(ReadOnlySpan<byte> keyUtf8, ReadOnlySpan<byte> valueBytes)
{
    var buffer = new ArrayBufferWriter<byte>();
    var writer = new Utf8JsonWriter(buffer);
    writer.WriteStartObject();
    writer.WritePropertyName(keyUtf8);
    writer.WriteRawValue(valueBytes, skipInputValidation: true);
    writer.WriteEndObject();
    writer.Flush();
    return new JsonRawBytes(buffer.WrittenMemory);
}
```

Uses `Utf8JsonWriter` for correct JSON encoding (Native AOT compatible, no reflection).
`skipInputValidation: true` is safe because `valueBytes` originates from `Utf8JsonReader`.

#### LastKeySegment (private)

```csharp
private static string LastKeySegment(IReadOnlyList<string> keyPath)
{
    for (var i = keyPath.Count - 1; i >= 0; i--)
        if (!keyPath[i].StartsWith('['))
            return keyPath[i];
    return "value"; // fallback: all segments are index segments
}
```

### 3.8 ModeController (modified)

**File:** `src/App/Tui/Ui/ModeController.cs`

#### Update `DrillDown()` — Phase 1 row creation

```csharp
public Result DrillDown(SingleDrillDownRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    var result = DrillDownSchemaExtractor.ExtractFromNode(request.NodeBytes, request.Format);
    if (result.IsFailure)
        return Results.Failure(result.Error);

    var children = result.Value.childRawValues;
    var rows = new FocusedTableRow[children.Count];
    for (var i = 0; i < children.Count; i++)
        rows[i] = new FocusedTableRow(children[i], $"[{i}]");

    _state.DrillDown = new DrillDownState(rows, result.Value.schema);
    _state.CurrentMode = ViewMode.FocusedTable;
    return Results.Success();
}
```

#### New `FullAggregationDrillDownAsync()`

Returns `Result<DrillDownState>` rather than mutating `AppState`. All state mutation is
deferred to the UI thread in `ViewManager` (see Section 3.9).

```csharp
/// <summary>
/// Executes the DrillDown Phase 2 file scan on a background thread and returns the result.
/// Does not mutate AppState — the caller is responsible for applying the result on the UI thread.
/// </summary>
public async ValueTask<Result<DrillDownState>> FullAggregationDrillDownAsync(FullAggregationDrillDownRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    // Capture by value before Task.Run to avoid reading _state on the thread-pool thread.
    var filePath = _state.CurrentFilePath;
    var ct = _state.Cts.Token;

    var result = await Task.Run(() =>
        FullAggregationScanner.Scan(filePath, request.Format, request.KeyPath, ct), ct);

    if (result.IsFailure)
        return Results.Failure<DrillDownState>(result.Error);

    return Results.Success(new DrillDownState(result.Value.rows, result.Value.schema));
}
```

Note: `FullAggregationDrillDownAsync` always suspends via `Task.Run` and never completes
synchronously, so `ValueTask` provides no allocation benefit over `Task` here. `ValueTask` is
retained for API consistency with other async `ModeController` methods.

**Current limitation:** `FullAggregationScanner.Scan` accumulates all `rows` and the schema in
memory and returns them only once, after the entire file has been scanned. The table has no
visibility into the scan until it is 100% complete, so for very large files (millions of
records) the UI shows no change for the whole scan duration.

**Future optimization:** streaming partial results (new rows/columns) to the table as the scan
progresses would require `Scan` to report progress incrementally (e.g., via a throttled
callback) and `DrillDownState` to change from a one-shot immutable result into a structure the
UI thread can append to over time. Deferred to a future phase (see Scope).

### 3.9 ViewManager (modified)

**File:** `src/App/Tui/Ui/ViewManager.cs`

All mutations of `_state.DrillDown` and `_state.CurrentMode` are performed inside
`_uiThreadInvoke` to eliminate cross-thread writes.

```csharp
/// <summary>
/// Orchestrates the DrillDown Phase 2 transition: offloads file scan to a background thread
/// via ModeController, then applies state and switches to FocusedTable view on the UI thread.
/// </summary>
internal async ValueTask FullAggregationDrillDownAsync(FullAggregationDrillDownRequest request)
{
    var result = await _modeController.FullAggregationDrillDownAsync(request);
    _uiThreadInvoke(() =>
    {
        if (result.IsFailure)
        {
            ShowError(result.Error);
            return;
        }

        _state.DrillDown = result.Value;
        _state.CurrentMode = ViewMode.FocusedTable;
        SwitchToFocusedTable(result.Value);
    });
}
```

`SwitchToFocusedTable(DrillDownState)` is unchanged.

### 3.10 AppKeyHandler (modified)

**File:** `src/App/Tui/Ui/AppKeyHandler.cs` — `HandleActionMenu()` tree-view branch

```
if currentView is MorphTreeView tv:
    if tv.SelectedObject is not ITreeNode selectedNode → return false

    var format = FormatDetector.Detect(_state.CurrentFilePath)
    if format.IsFailure → show error, return false

    if format.Value == DataFormat.JsonObject:
        // Phase 1: retain existing behavior — only JsonArrayTreeNode with JsonObject children
        if selectedNode is not JsonArrayTreeNode arrayNode → return false
        if arrayNode.Children.Count == 0 → return false
        if any child is not JsonObjectTreeNode → return false

        var request = new SingleDrillDownRequest(
            Format: format.Value,
            NodeBytes: arrayNode.RawJson)

        void onConfirmed(string _) → _viewManager.DrillDown(request)

    else:
        // Phase 2: JSON Lines / JSON Array — any node type, always full scan
        var keyPath = BuildKeyPath(selectedNode)

        var request = new FullAggregationDrillDownRequest(
            Format: format.Value,
            KeyPath: keyPath)

        void onConfirmed(string _)
            _ = _viewManager.FullAggregationDrillDownAsync(request)
                    .AsTask()
                    .ContinueWith(HandleTaskError, TaskScheduler.Default)

    var dialog = new ActionMenuDialog(["DrillDown"], onConfirmed)
    _app.Run(dialog)
    return true
```

`HandleTaskError` follows the same error-reporting pattern used elsewhere in `AppKeyHandler`
for fire-and-forget async operations.

#### BuildKeyPath (private static)

Traverses the `ParentNode` chain from the selected node up to the root, collecting `KeyName`
segments in bottom-up order, then reverses to produce a root-to-leaf path.

```csharp
private static IReadOnlyList<string> BuildKeyPath(ITreeNode node)
{
    List<string> segments = [];
    ITreeNode? current = node;

    while (current is JsonObjectTreeNode or JsonArrayTreeNode or JsonValueTreeNode)
    {
        var (keyName, parent) = current switch
        {
            JsonObjectTreeNode obj => (obj.KeyName, obj.ParentNode),
            JsonArrayTreeNode arr  => (arr.KeyName, arr.ParentNode),
            JsonValueTreeNode val  => (val.KeyName, val.ParentNode),
            _                      => throw new UnreachableException(),
        };

        if (keyName is not null)
            segments.Add(keyName);

        current = parent;
    }

    segments.Reverse();
    return segments;
}
```

Root nodes have `ParentNode = null` and `KeyName = null`; the loop stops when `current` is
none of the three handled types (i.e., root sentinel or unknown type).

---

## 4. Thread Safety

| Component | Safety | Mechanism |
|-----------|--------|-----------|
| `FocusedTableRow` | Thread-safe | Immutable record struct |
| `SchemaScanner` | Thread-safe | Stateless static class; holds no fields, operates only on collections passed in by the caller |
| `FullAggregationScanner.Scan` | Thread-safe | Stateless static method; all state is local |
| `DrillDownState` | UI thread only | Immutable after construction; consumed only on UI thread |
| `ModeController.FullAggregationDrillDownAsync` | Safe | Returns `Result<DrillDownState>` without mutating `AppState`; no cross-thread writes |
| `ViewManager.FullAggregationDrillDownAsync` | Safe | `_state.DrillDown` and `_state.CurrentMode` assigned exclusively inside `_uiThreadInvoke` |
| `JsonObjectTreeNode / JsonArrayTreeNode / JsonValueTreeNode ParentNode` | Thread-safe | `init`-only; set once at construction; read-only thereafter |

---

## 5. Test Plan

### 5.1 FullAggregationScannerTests

Scenarios that share identical logic for `DataFormat.JsonLines` and `DataFormat.JsonArray`
must be written as `[Theory]` with `[InlineData(DataFormat.JsonLines)]` /
`[InlineData(DataFormat.JsonArray)]`, not as duplicate `[Fact]` methods.

**Path traversal and leaf collection:**

| Scenario | Test kind | Expected |
|----------|-----------|----------|
| JSONObject leaf — key in all records → 1 row per record | `[Theory]` | Correct rows; `#` = `{pos}` |
| JSONArray-of-JSONObject leaf — key in all records → rows per element | `[Theory]` | Correct rows; `#` = `{pos}:{i}` |
| JSONArray-of-Primitive leaf — key in all records → rows with `value` column | `[Theory]` | Correct rows; `#` = `{pos}:{i}`; column = `"value"` |
| Primitive leaf (scalar value) | `[Theory]` | Correct rows; `#` = `{pos}`; column = last key segment |
| `[n]` segment in path → same output as selecting the parent array directly | `[Theory]` | Identical rows from `["orders"]` and `["orders", "[0]"]` |
| Deep path with 2 `[n]` segments → `#` has 2 `:` separators | `[Theory]` | `#` = `{pos}:{i}:{j}` |
| Key missing in some records → skipped silently | `[Theory]` | Only rows from matching records |
| Key segment followed by non-object token → skip record silently | `[Theory]` | Only rows from matching records |
| Index segment followed by non-array token → skip record silently | `[Theory]` | Only rows from matching records |
| No records match → `Result.Failure` | `[Theory]` | `"No matching records found."` |
| JSON Object format → `Result.Failure` | `[Fact]` | Error about unsupported format |
| All matched leaf objects are `{}` (empty) → `Result.Failure` | `[Theory]` | `"All child objects have no keys"` |
| Union schema: varying keys across records → all present; missing nullable | `[Theory]` | All keys; nullable where absent |
| Cancellation mid-scan → `OperationCanceledException` propagates | `[Theory]` | Exception thrown |
| Nested object/array value in object row → rendered as `{...}` / `[...]` | `[Theory]` | Cell value is `{...}` or `[...]` |
| Leaf value is JSON `null` → one null row emitted (Section 1.6) | `[Theory]` | One row with null cell values; not skipped |
| Empty array `[]` at leaf position → record contributes 0 rows; others still collected | `[Theory]` | Rows from non-empty-array records only |
| Mixed array: some elements are objects, others are primitives | `[Theory]` | Object elements produce object rows; primitive elements produce `{"value": x}` rows |
| UTF-8 multi-byte property name as column identifier | `[Theory]` | Column name round-trips correctly through `Encoding.UTF8.GetBytes` / `Utf8JsonReader` |

### 5.2 FocusedTableSourceTests (updated)

| Scenario | Expected |
|----------|----------|
| `Rows` = row count | Correct |
| `Columns` = schema.ColumnCount + 1 | Correct |
| `ColumnNames[0]` = `"#"` | Correct |
| `this[row, 0]` = `row.HashValue` | Correct (pre-computed, format-agnostic) |
| `this[row, n]` (n > 0) delegates to `JsonObjectCellExtractor` | Correct cell value |
| `row < 0` or `row >= Rows` | `ArgumentOutOfRangeException` |
| `col < 0` or `col >= Columns` | `ArgumentOutOfRangeException` |

### 5.3 ModeController — Phase 1 DrillDown (updated)

Verify that `DrillDown()` still produces correct `DrillDownState.Rows`:

| Scenario | Expected `HashValue` for child 0 |
|----------|----------------------------------|
| JSON Object, 3 children | `"[0]"` |
| JSON Object, 1 child | `"[0]"` |

---

## 6. Files to Create / Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/DrillDown/FocusedTableRow.cs` | Create | Value type: child bytes + pre-computed hash value |
| `src/Engine/IO/DrillDown/SchemaScanner.cs` | Create | Stateless static helpers (`ScanObject`, `RegisterKeyIfNew`, `IncrementObservationCounts`, `BuildTableSchema`) shared by `DrillDownSchemaExtractor` and `FullAggregationScanner` |
| `src/Engine/IO/DrillDown/FullAggregationScanner.cs` | Create | Full file scan: traverse KeyPath; collect rows for all leaf types; build union schema |
| `src/Engine/IO/DrillDown/DrillDownSchemaExtractor.cs` | Modify | `BuildSchema` delegates to `SchemaScanner`; `ScanObject`/`RegisterKeyIfNew`/`IncrementObservationCounts`/`MergeColumnType` move out |
| `src/App/Tui/DrillDownState.cs` | Modify | Replace `ChildRawValues / Format / RecordPosition` with `IReadOnlyList<FocusedTableRow> Rows` |
| `src/App/Tui/Ui/Views/FocusedTableSource.cs` | Modify | Accept new `DrillDownState`; replace `FormatHashColumn` with `_rows[row].HashValue` |
| `src/App/Tui/DrillDownRequest.cs` | Modify | Split the single record into an abstract `DrillDownRequest` base plus `SingleDrillDownRequest` and `FullAggregationDrillDownRequest` |
| `src/App/Tui/Ui/Views/JsonObjectTreeNode.cs` | Modify | Add `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonArrayTreeNode.cs` | Modify | Add `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonValueTreeNode.cs` | Modify | Add `public string? KeyName { get; init; }` and `public ITreeNode? ParentNode { get; init; }` |
| `src/App/Tui/Ui/Views/JsonTreeNodeHelper.cs` | Modify | Add `ITreeNode? parentNode` parameter to `CreateChildNode`, `CreateNestedObjectNode`, `CreateNestedArrayNode`; pass through to node constructors |
| `src/App/Tui/Ui/ModeController.cs` | Modify | Update `DrillDown()` to build rows inline; add `FullAggregationDrillDownAsync()` returning `Result<DrillDownState>` |
| `src/App/Tui/Ui/ViewManager.cs` | Modify | Add `FullAggregationDrillDownAsync()`; assign state inside `_uiThreadInvoke` |
| `src/App/Tui/Ui/AppKeyHandler.cs` | Modify | Single "DrillDown" option; add `BuildKeyPath`; dispatch Phase 1 (JSON Object) vs Phase 2 (JSON Lines/Array) |
| `tests/.../FocusedTableSourceTests.cs` | Modify | Update for new `DrillDownState` shape |
| `tests/.../FullAggregationScannerTests.cs` | Create | Scanner unit tests |

---

## 7. Acceptance Criteria

- [ ] `x` key on any tree node (JSON Lines / JSON Array) opens ActionMenu with a single "DrillDown" option
- [ ] `x` key on a non-JsonArrayTreeNode node in a JSON Object file does NOT show the ActionMenu
- [ ] "DrillDown" on a JSONObject leaf collects one row per record; `#` = record position
- [ ] "DrillDown" on a JSONArray-of-JSONObject leaf collects one row per child element; `#` = `{pos}:{i}`
- [ ] "DrillDown" on a JSONArray-of-Primitive leaf shows a single `value` column; `#` = `{pos}:{i}`
- [ ] "DrillDown" on a Primitive leaf shows a single column named after the key; `#` = record position
- [ ] Selecting `orders` and `orders[0]` produces identical DrillDown output
- [ ] Records where the key path is absent or the token type mismatches are silently skipped
- [ ] If no records produce any rows, an error dialog is shown
- [ ] `#` column depth (number of `:` separators) equals the number of arrays traversed along the path
- [ ] Phase 1 "DrillDown" on JSON Object format continues to work correctly after the refactor
- [ ] `FocusedTableSource` and `DrillDownState` use `FocusedTableRow` (no per-source Format/RecordPosition)
- [ ] Unit tests pass for `FullAggregationScanner` and updated `FocusedTableSourceTests`
