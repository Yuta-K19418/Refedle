# DrillDown Command — Phase 1 Design Document

## Scope

Implements the **DrillDown command** (`x` → ActionMenu → DrillDown) that converts a selected
JSON Array node in Explorer (Tree) mode into a **FocusedTable**: a tabular view displaying its
child objects as rows.

**This document covers Phase 1 only.** The DrillDown command is delivered in two phases:

| Phase | Scope | Description |
|-------|-------|-------------|
| **Phase 1** (this document) | Selected node only | Displays the children of the selected `JsonArrayTreeNode` as a table. No file scan required. Supported formats: JSON Lines, JSON Array, JSON Object. |
| **Phase 2** (future document) | Full aggregation | Scans all rows/elements in the file and aggregates values at the same key path across records. Supported formats: JSON Lines, JSON Array only (JSON Object excluded — single document, nothing to aggregate). |

**Out of scope (deferred):**
- Phase 2 implementation (full-file aggregation)
- Back / Undo navigation (Backspace to return to Tree)
- Breadcrumb path display

---

## 1. Requirements

### 1.1 Functional

- `x` key in Tree mode opens ActionMenu; DrillDown appears as a selectable action
- DrillDown is enabled only when the selected node is a `JsonArrayTreeNode` **and** all its direct children are `JsonObjectTreeNode`
- DrillDown is **disabled** when children include primitives, nested arrays, or mixed types
- Each child `JsonObjectTreeNode` becomes one row; object keys become columns (union across all children)
- Display is **1 level deep**: primitive values as-is; nested Objects as `{...}`; nested Arrays as `[...]`
- A `#` column is always the first column showing the source position (see Section 1.2 for per-format details)
- `ViewMode.FocusedTable` is added for the resulting table state

### 1.2 Specifications

#### Enable Condition

| Condition | DrillDown |
|-----------|-----------|
| Selected node is `JsonArrayTreeNode` and all direct children are `JsonObject` | **Enabled** |
| Selected node is `JsonObjectTreeNode` | Disabled |
| Selected node is `JsonArrayTreeNode` with primitive children | Disabled |
| Selected node is `JsonArrayTreeNode` with nested array children | Disabled |
| Selected node is `JsonArrayTreeNode` with mixed-type children | Disabled |

The format (JSON Lines / JSON Array / JSON Object) does not affect enable/disable logic — only the node type and its children matter.

#### `#` Column

The `#` value format differs by format, to ensure Phase 2 consistency:

| Format | `#` format | Example | Rationale |
|--------|-----------|---------|-----------|
| JSON Lines | `{lineNumber}:{childIndex}` | `22:0`, `22:1` | Line number identifies the parent record |
| JSON Array | `{elementIndex}:{childIndex}` | `5:0`, `5:1` | Element index identifies the parent record |
| JSON Object | `[{childIndex}]` | `[0]`, `[1]` | No parent record concept; child index only |

In Phase 2 (future), JSON Lines and JSON Array will aggregate across all rows, producing rows like `1:0`, `1:1`, `2:0`, `2:1`... — the same format as Phase 1. JSON Object is excluded from Phase 2 as it is a single document with no rows to aggregate across.

#### Cell Rendering Rules

| Value Type | Display |
|------------|---------|
| Primitive (string / number / bool) | Value as-is |
| Nested Object | `{...}` |
| Nested Array | `[...]` |
| Key absent | `<null>` |

> **Key distinction:** each child `JsonObjectTreeNode` is unpacked into columns.
> Values within those objects that are themselves Objects or Arrays are rendered as `{...}` / `[...]`.

#### Behavior

The table structure is identical regardless of file format. The selected `JsonArrayTreeNode`'s children are used directly — no file scan required. Only the `#` value differs by format.

Input (selected array value):
```json
[
  {"name": "Alice", "role": "admin",  "meta": {"dept": "Eng"}},
  {"name": "Bob",   "role": "viewer", "meta": {"dept": "HR"}}
]
```

**JSON Lines** (`#` = `{lineNumber}:{childIndex}`, e.g. selected node is `users` on line 22):

| #    | name  | role   | meta    |
|------|-------|--------|---------|
| 22:0 | Alice | admin  | `{...}` |
| 22:1 | Bob   | viewer | `{...}` |

**JSON Array** (`#` = `{elementIndex}:{childIndex}`, e.g. selected node is at element index 5):

| #   | name  | role   | meta    |
|-----|-------|--------|---------|
| 5:0 | Alice | admin  | `{...}` |
| 5:1 | Bob   | viewer | `{...}` |

**JSON Object** (`#` = `[{childIndex}]`):

| #   | name  | role   | meta    |
|-----|-------|--------|---------|
| [0] | Alice | admin  | `{...}` |
| [1] | Bob   | viewer | `{...}` |

`meta` is a nested object → rendered as `{...}` (1-level deep rule).

### 1.3 Non-Functional

- Zero allocation in cell extraction hot paths (`Utf8JsonReader` on `ReadOnlySpan<byte>`)
- Native AOT compatible (no reflection)
- All classes stay under 300 lines

---

## 2. Architecture

### 2.1 Data Flow

Phase 1 requires no file scan. All data comes from the already-loaded tree node bytes.

```
[User selects JsonArrayTreeNode in TreeView]
[User presses x → ActionMenu → DrillDown]
             ↓
AppKeyHandler.HandleActionMenu()  ← extended for MorphTreeView
  Reads: node.RawJson, node.RecordPosition, node.KeyName
  Builds: DrillDownRequest
             ↓
ViewManager.DrillDownAsync(DrillDownRequest)
             ↓
ModeController.DrillDownAsync(DrillDownRequest)
  Calls: DrillDownSchemaExtractor.ExtractFromNode(request.NodeBytes, request.Format)
  Stores: (schema, childValueBytes) → AppState
  Sets:   AppState.CurrentMode = ViewMode.FocusedTable
             ↓
ViewManager reads AppState → calls SwitchToFocusedTable()
             ↓
FocusedTableSource  (new, App)
  → renders cells via JsonObjectCellExtractor (moved/renamed from CellExtractor)
  → formats # column per format + RecordPosition
             ↓
FocusedTableView  (new, App)  →  Terminal.Gui TableView
```

### 2.2 New Components

| Component | Layer | File | Responsibility |
|-----------|-------|------|----------------|
| `JsonByteExtractor` | Engine | `src/Engine/IO/Json/JsonByteExtractor.cs` | Shared Engine-layer utility: extract raw bytes of a nested JSON value by depth-tracking |
| `DrillDownSchemaExtractor` | Engine | `src/Engine/IO/DrillDown/DrillDownSchemaExtractor.cs` | Parse selected array node bytes → schema + ordered child value list |
| `DrillDownRequest` | App | `src/App/Tui/DrillDownRequest.cs` | Data bag: format, key name, record position, raw node bytes |
| `FocusedTableSource` | App | `src/App/Tui/Ui/Views/FocusedTableSource.cs` | `ITableSource` backed by pre-materialized child value bytes; format-aware `#` column |
| `FocusedTableView` | App | `src/App/Tui/Ui/Views/FocusedTableView.cs` | `MorphTableView` subclass for FocusedTable |

> **Note:** `DrillDownCellExtractor` is **not** a new class. Cell extraction reuses
> `JsonObjectCellExtractor` (see S-1 fix in Section 2.3).

### 2.3 Modified Files

| File | Change |
|------|--------|
| `src/Engine/IO/JsonLines/CellExtractor.cs` | Move to `src/Engine/IO/Json/JsonObjectCellExtractor.cs`; update namespace to `Refedle.Engine.IO.Json`; keep identical logic |
| `src/App/Tui/ViewMode.cs` | Add `FocusedTable` |
| `src/App/Tui/Ui/ViewManager.cs` | Add `DrillDownAsync()` and `SwitchToFocusedTable()` (with `OnSchemaRefined = null` cleanup); update `RefreshStatusBarHints` to exclude `FocusedTable` mode and include `MorphTreeView` |
| `src/App/Tui/Ui/ModeController.cs` | Add `DrillDownAsync()` |
| `src/App/Tui/Ui/AppKeyHandler.cs` | Extend `HandleActionMenu()` for `MorphTreeView` |
| `src/App/Tui/AppState.cs` | Add `DrillDownChildValueBytes`, `DrillDownSchema`, `DrillDownFormat`, `DrillDownRecordPosition` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonObjectTreeNode.cs` | Add `KeyName` and `RecordPosition` properties; propagate `RecordPosition` in `LoadChildren`; call `JsonByteExtractor.ExtractNestedBytes` via `JsonTreeNodeHelper` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonArrayTreeNode.cs` | Add `KeyName`, `RecordPosition`, and `RawJson` properties; propagate `RecordPosition` in `LoadChildren` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonTreeNodeHelper.cs` | Accept `recordPosition` parameter; set `KeyName` and `RecordPosition` on nested nodes; delegate byte slicing to `JsonByteExtractor.ExtractNestedBytes` instead of the current private `ExtractNestedBytes` |
| `src/App/Tui/Ui/Views/JsonObjectTreeView.cs` | Set `KeyName = key` on `JsonArrayTreeNode` / `JsonObjectTreeNode` created in `CreateKeyNode` (Phase 1 consistency; required for Phase 2 breadcrumb) |
| `src/App/Tui/Ui/Views/JsonRangeTreeNodes/JsonLinesRangeTreeNode.cs` | Set `RecordPosition` (1-based line number) on root nodes created in `CreateLineNode` |
| `src/App/Tui/Ui/Views/JsonRangeTreeNodes/JsonArrayRangeTreeNode.cs` | Set `RecordPosition` (0-based element index) on root nodes created in `CreateElementNode` |
| `src/App/Tui/Workers/Schema/JsonLines/...` (callers of `CellExtractor`) | Update `using` to reference `JsonObjectCellExtractor` in new namespace |

### 2.4 Reused Components

| Component | Usage |
|-----------|-------|
| `JsonObjectCellExtractor` (renamed from `CellExtractor`) | Object cell extraction for both JSON Lines table source and `FocusedTableSource` |
| `TableSchema` / `ColumnSchema` | Schema output types |
| `TypeInferrer` | Per-column type inference during `ExtractFromNode` |
| `ActionMenuDialog` | Extended with DrillDown action |

---

## 3. Component Design

### 3.1 DrillDownRequest

**File:** `src/App/Tui/DrillDownRequest.cs`
**Namespace:** `Refedle.App.Tui`
**Estimated size:** ~10 lines

Primary constructor form per project standards (no validation logic required):

```csharp
/// <param name="Format">Source file format.</param>
/// <param name="NodeBytes">Raw bytes of the selected JsonArrayTreeNode's JSON value.</param>
/// <param name="KeyName">
///   Property name this array node represents (e.g. "tags").
///   Null when the selected node is a root-level array (JSON Lines line / JSON Array element).
/// </param>
/// <param name="RecordPosition">
///   1-based line number (JSON Lines) or 0-based element index (JSON Array) of the ancestor
///   root record. Null for JSON Object (no parent record concept).
/// </param>
internal sealed record DrillDownRequest(
    DataFormat Format,
    ReadOnlyMemory<byte> NodeBytes,
    string? KeyName = null,
    long? RecordPosition = null);
```

### 3.2 Tree Node Extensions

#### RecordPosition Propagation

`JsonLinesRangeTreeNode` and `JsonArrayRangeTreeNode` know the position of each root node they
create. That position must be threaded down to any nested `JsonArrayTreeNode` so the DrillDown
handler can read it without tree traversal.

**`JsonLinesRangeTreeNode.CreateLineNode` — change:**
```csharp
// lineIndex is long (0-based); RecordPosition stores the 1-based line number as long.
if (reader.TokenType == JsonTokenType.StartObject)
    return new JsonObjectTreeNode(lineBytes, prefix) { RecordPosition = lineIndex + 1 };
if (reader.TokenType == JsonTokenType.StartArray)
    return new JsonArrayTreeNode(lineBytes, prefix) { RecordPosition = lineIndex + 1 };
```

**`JsonArrayRangeTreeNode.CreateElementNode` — change:**
```csharp
// index is long (0-based).
if (reader.TokenType == JsonTokenType.StartObject)
    return new JsonObjectTreeNode(bytes, prefix) { RecordPosition = index };
if (reader.TokenType == JsonTokenType.StartArray)
    return new JsonArrayTreeNode(bytes, prefix) { RecordPosition = index };
```

**`JsonObjectTreeNode` — additions:**
```csharp
/// Property name this node represents. Null for root-level nodes.
public string? KeyName { get; init; }

/// Ancestor root record position (1-based line# for JSON Lines; 0-based element# for JSON Array).
/// Null for JSON Object format (no root record concept) and for nodes not yet assigned.
public long? RecordPosition { get; init; }
```

`LoadChildren` passes `RecordPosition` to `JsonTreeNodeHelper.CreateChildNode`:
```csharp
var childNode = JsonTreeNodeHelper.CreateChildNode(ref reader, propertyName, _rawJson, RecordPosition);
```

**`JsonArrayTreeNode` — additions:**
```csharp
/// Property name or index-label this node represents (e.g. "tags", "[0]").
/// Null for root-level array nodes.
public string? KeyName { get; init; }

/// Ancestor root record position. Same semantics as JsonObjectTreeNode.RecordPosition.
public long? RecordPosition { get; init; }

/// Exposes the raw JSON bytes for DrillDown (AppKeyHandler reads this directly).
internal ReadOnlyMemory<byte> RawJson => _rawJson;
```

`LoadChildren` similarly propagates `RecordPosition` to children.

**`JsonTreeNodeHelper` — changes:**
`CreateChildNode`, `CreateNestedObjectNode`, and `CreateNestedArrayNode` each gain an
`int? recordPosition` parameter. The private `ExtractNestedBytes` method is removed; calls are
delegated to `JsonByteExtractor.ExtractNestedBytes` (Engine layer, no circular dependency).
The nested node factories set both `KeyName = label` and `RecordPosition = recordPosition`:
```csharp
private static JsonArrayTreeNode CreateNestedArrayNode(
    ref Utf8JsonReader reader,
    string label,
    ReadOnlyMemory<byte> rawJson,
    int? recordPosition)
{
    var arrayBytes = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);
    return new JsonArrayTreeNode(arrayBytes, $"{label}: ")
    {
        KeyName = label,
        RecordPosition = recordPosition,
    };
}
```

**`JsonObjectTreeView.CreateKeyNode` — change:**
Set `KeyName = key` on nodes created here so that JSON Object top-level arrays are consistent
with nested nodes created via `JsonTreeNodeHelper`:
```csharp
if (reader.TokenType == JsonTokenType.StartObject)
    return new JsonObjectTreeNode(valueBytes, prefix) { KeyName = key };
if (reader.TokenType == JsonTokenType.StartArray)
    return new JsonArrayTreeNode(valueBytes, prefix) { KeyName = key };
```
(`RecordPosition` remains `null` for JSON Object format — correct per Section 1.2.)

### 3.3 JsonByteExtractor (new Engine utility)

**File:** `src/Engine/IO/Json/JsonByteExtractor.cs`
**Namespace:** `Refedle.Engine.IO.Json`
**Estimated size:** ~30 lines

Shared Engine-layer utility for extracting raw bytes of a nested JSON value. Extracted here so
both `JsonTreeNodeHelper` (App layer calling Engine) and `DrillDownSchemaExtractor` (Engine
layer) can use the same implementation without a circular dependency.

`public` visibility is required: callers in the App assembly (`JsonTreeNodeHelper`) access this
type cross-assembly.

```csharp
public static class JsonByteExtractor
{
    /// <summary>
    /// Advances <paramref name="reader"/> past the current nested value (Object or Array)
    /// and returns a slice of <paramref name="rawJson"/> covering it exactly.
    /// Reader must be positioned at a StartObject or StartArray token.
    /// </summary>
    public static ReadOnlyMemory<byte> ExtractNestedBytes(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> rawJson);
}
```

### 3.4 JsonObjectCellExtractor (moved from CellExtractor)

**Original file:** `src/Engine/IO/JsonLines/CellExtractor.cs`  
**New file:** `src/Engine/IO/Json/JsonObjectCellExtractor.cs`  
**Namespace:** `Refedle.Engine.IO.Json`  
**Estimated change:** rename class + update namespace; logic unchanged

Extracts a single cell value from a raw JSON Object value one level deep. Previously used only
by JSON Lines table source; moved here so `FocusedTableSource` can reuse it without a
namespace-misleading `JsonLines` dependency.

The public API surface and cell rendering rules remain identical to the existing `CellExtractor`:

| JSON Token | Output |
|------------|--------|
| String | Raw string value (no quotes) |
| Integer | Formatted via `Utf8Parser.TryParse` → `long` |
| Decimal | Formatted via `Utf8Parser.TryParse` → `double` |
| `true` / `false` | `"True"` / `"False"` |
| `null` | `"<null>"` |
| `{ ... }` | `"{...}"` |
| `[ ... ]` | `"[...]"` |
| Missing key | `"<null>"` |
| Malformed JSON | `"<error>"` |

### 3.5 DrillDownSchemaExtractor

**File:** `src/Engine/IO/DrillDown/DrillDownSchemaExtractor.cs`
**Namespace:** `Refedle.Engine.IO.DrillDown`
**Estimated size:** ~100 lines

Parses the selected `JsonArrayTreeNode`'s raw bytes in memory — no file scan required.
Returns the inferred schema and an ordered list of child object bytes.

#### API

`public` visibility is required: `ModeController` in the App assembly calls this cross-assembly.

```csharp
public static class DrillDownSchemaExtractor
{
    /// <summary>
    /// Parses <paramref name="nodeBytes"/> as a JSON array whose direct children must all be
    /// JSON Objects. Infers a <see cref="TableSchema"/> (union of top-level keys) and returns
    /// the ordered child value bytes.
    /// Returns <c>Failure</c> when children include non-Objects, the array is empty, or the
    /// JSON is malformed.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<ReadOnlyMemory<byte>> childValueBytes)>
        ExtractFromNode(ReadOnlyMemory<byte> nodeBytes, DataFormat format);
}
```

#### Schema Inference Logic

1. Parse `nodeBytes` as a JSON array
2. For each element, call `JsonByteExtractor.ExtractNestedBytes` (Engine layer — no circular dependency) to slice the child object bytes
3. Verify all elements start with `{`; return `Failure` if any are non-Object or if the array is empty
4. Compute the union of top-level keys across all child objects (first-seen order)
5. **If the key union is empty** (e.g., `[{}, {}]`), return `Result.Failure("All child objects have no keys")` — `TableSchema` requires at least one column
6. Column types inferred via `TypeInferrer`; rows missing a key → column marked nullable
7. Return `(TableSchema { Columns, SourceFormat = format }, childValueBytes)`

### 3.6 FocusedTableSource

**File:** `src/App/Tui/Ui/Views/FocusedTableSource.cs`
**Namespace:** `Refedle.App.Tui.Ui.Views`
**Estimated size:** ~90 lines

`ITableSource` backed by pre-materialized child object bytes; renders the `#` column using
format-specific formatting per Section 1.2.

#### Constructor

```csharp
internal sealed class FocusedTableSource : ITableSource
{
    internal FocusedTableSource(
        IReadOnlyList<ReadOnlyMemory<byte>> childValueBytes,
        TableSchema schema,
        DataFormat format,
        long? recordPosition);
}
```

#### Column Layout

| Index | Name | Content |
|-------|------|---------|
| 0 | `"#"` | Format-specific position string (see below) |
| 1..N | Schema column names (top-level object keys) | Cell values via `JsonObjectCellExtractor.ExtractCell` |

- `Columns = schema.ColumnCount + 1`
- `Rows = childValueBytes.Count`

#### `#` Column Formatting

| Format | `RecordPosition` | Output for child index `i` |
|--------|-----------------|----------------------------|
| JSON Lines | 1-based line number (non-null) | `{recordPosition}:{i}` e.g. `22:0` |
| JSON Array | 0-based element index (non-null) | `{recordPosition}:{i}` e.g. `5:0` |
| JSON Object | null | `[{i}]` e.g. `[0]` |

#### Bounds Checking

`this[row, col]` throws `ArgumentOutOfRangeException` when:
- `row < 0 || row >= Rows`, or
- `col < 0 || col >= Columns`

Guard clauses are applied before any field access.

#### Indexer

```
this[row, col]:
  guard: row in [0, Rows) and col in [0, Columns) → throw ArgumentOutOfRangeException if violated
  col == 0 → FormatHashColumn(row)          // see # Column Formatting above
  col  > 0 → JsonObjectCellExtractor.ExtractCell(
                  childValueBytes[row].Span,
                  _columnNamesUtf8[col - 1])
```

#### Internal State

| Field | Type | Purpose |
|-------|------|---------|
| `_childValueBytes` | `IReadOnlyList<ReadOnlyMemory<byte>>` | Ordered child object bytes |
| `_schema` | `TableSchema` | Column definitions |
| `_columnNamesUtf8` | `byte[][]` | Pre-encoded UTF-8 column names for zero-alloc extraction |
| `_format` | `DataFormat` | Determines `#` column format |
| `_recordPosition` | `long?` | Parent record position; null for JSON Object |

### 3.7 FocusedTableView

**File:** `src/App/Tui/Ui/Views/FocusedTableView.cs`
**Namespace:** `Refedle.App.Tui.Ui.Views`
**Estimated size:** ~5 lines

Empty `MorphTableView` subclass, following the same pattern as `JsonLinesTableView`.

```csharp
/// <summary>TableView for DrillDown (FocusedTable) mode.</summary>
internal sealed class FocusedTableView : MorphTableView;
```

### 3.8 AppKeyHandler Extension

`HandleActionMenu()` is extended to also handle `MorphTreeView`. The existing `MorphTableView`
branch is retained as-is; the tree-view branch is added as a separate guard:

```
// ── Existing branch (unchanged) ──────────────────────────────────────
if currentView is MorphTableView mt:
    if mt.Table is null || mt.GetRawColumnName is null || mt.OnMorphAction is null || mt.Value is null
        → return false
    var handler = new ColumnActionHandler(...)
    using var dialog = new ActionMenuDialog(...)
    _app.Run(dialog)
    return true

// ── New branch (DrillDown) ────────────────────────────────────────────
if currentView is MorphTreeView tv:
    if tv.SelectedObject is not JsonArrayTreeNode arrayNode → return false

    // Trigger lazy load and verify enable condition
    var children = arrayNode.Children
    if children.Count == 0 → return false
    if any child is not JsonObjectTreeNode → return false

    var format = FormatDetector.Detect(_state.CurrentFilePath)
    if format.IsFailure → show error, return false

    var request = new DrillDownRequest(
        Format: format.Value,
        NodeBytes: arrayNode.RawJson,
        KeyName: arrayNode.KeyName,
        RecordPosition: arrayNode.RecordPosition)

    Show ActionMenuDialog with ["DrillDown"] as the only option
    On confirm → _ = _viewManager.DrillDownAsync(request).ContinueWith(handleErrors, TaskScheduler.Default)
    return true

return false
```

`IsGlobalShortcut` already includes `KeyCode.X`, so no change is needed there.

### 3.9 ModeController Extension

```csharp
/// <summary>
/// Executes the DrillDown command for the given request.
/// Parses the selected node's bytes in memory, infers schema, and stores results in AppState.
/// </summary>
public ValueTask<Result> DrillDownAsync(DrillDownRequest request)
```

Implementation:

```
result = DrillDownSchemaExtractor.ExtractFromNode(request.NodeBytes, request.Format)
if result.IsFailure → return Result.Failure(result.Error)

_state.DrillDownChildValueBytes = result.Value.childValueBytes
_state.DrillDownSchema = result.Value.schema
_state.DrillDownFormat = request.Format
_state.DrillDownRecordPosition = request.RecordPosition
_state.CurrentMode = ViewMode.FocusedTable
return Result.Success()
```

No format-specific dispatch: all formats use `ExtractFromNode` since Phase 1 reads only the
already-loaded node bytes.

Because `ExtractFromNode` is pure in-memory parsing of small byte slices (no I/O), this method
can complete synchronously. Return `ValueTask.FromResult(...)` to avoid unnecessary task allocation.

### 3.10 ViewManager Extension

```csharp
/// <summary>
/// Orchestrates the DrillDown transition: delegates schema extraction to ModeController,
/// then switches to FocusedTable view on the UI thread.
/// </summary>
internal async Task DrillDownAsync(DrillDownRequest request)
```

```
result = await _modeController.DrillDownAsync(request)
_uiThreadInvoke(() =>
{
    if result.IsFailure:
        ShowError(result.Error)
        return

    // Declaration-pattern unwrapping: Debug.Assert does not satisfy C# nullable flow
    // analysis; ! is forbidden by project rules. Use is-not-{} + UnreachableException.
    if _state.DrillDownChildValueBytes is not { } childValueBytes
       || _state.DrillDownSchema is not { } schema
       || _state.DrillDownFormat is not { } format:
        throw new UnreachableException(
            "ModeController.DrillDownAsync must set all DrillDown state fields on success.")

    SwitchToFocusedTable(childValueBytes, schema, format, _state.DrillDownRecordPosition)
})
```

```csharp
[SuppressMessage("Reliability", "CA2000", Justification = "Owned by container via SwapView.")]
internal void SwitchToFocusedTable(
    IReadOnlyList<ReadOnlyMemory<byte>> childValueBytes,
    TableSchema schema,
    DataFormat format,
    long? recordPosition)
```

Creates `FocusedTableSource(childValueBytes, schema, format, recordPosition)` and
`FocusedTableView`, then performs the following sequence:

```csharp
_state.OnSchemaRefined = null;          // clear stale callback before SwapView
// FocusedTableSource rows are fully materialised at construction; no IRowIndexer needed.
view.SetSelection(0, 0, false);         // position cursor at first cell
view.Update();
SwapView(view);
view.SetFocus();
RefreshStatusBarHints();
```

`SetInitialSelectionWhenReady` (used by other `SwitchTo*` methods) requires an `IRowIndexer`
and must **not** be called here.

#### RefreshStatusBarHints update

`RefreshStatusBarHints` must show `"x:Menu"` when a tree view is active, but **not** when
`ViewMode.FocusedTable` is active (`FocusedTableView` is a `MorphTableView` subclass but
`OnMorphAction` is not wired up, so pressing `x` would silently no-op):

```csharp
if ((GetCurrentView() is MorphTableView && _state.CurrentMode != ViewMode.FocusedTable)
    || GetCurrentView() is MorphTreeView)
{
    hints.Add("x:Menu");
}
```

---

## 4. Thread Safety

| Component | Safety | Mechanism |
|-----------|--------|-----------|
| `JsonObjectCellExtractor` | Thread-safe | Stateless static method on `ReadOnlySpan<byte>` |
| `DrillDownSchemaExtractor` | Thread-safe | Stateless static method; no shared state |
| `FocusedTableSource` | UI thread only | Immutable after construction; all fields set in constructor |
| `ModeController.DrillDownAsync` | Synchronous in practice | Pure in-memory parsing; result delivered to UI thread via `_uiThreadInvoke` in `ViewManager` |

---

## 5. Test Plan

### 5.1 JsonObjectCellExtractorTests

> `JsonObjectCellExtractor` replaces the former `DrillDownCellExtractor`. These tests cover
> the shared extractor from the DrillDown perspective; existing `CellExtractorTests` are
> updated in-place when the class is renamed.

| Scenario | Expected |
|----------|----------|
| Extract string from Object | Raw string value (no quotes) |
| Extract integer from Object | Formatted string |
| Extract decimal from Object | Formatted string |
| Extract `true` / `false` from Object | `"True"` / `"False"` |
| Extract null from Object | `"<null>"` |
| Extract nested Object value | `"{...}"` |
| Extract nested Array value | `"[...]"` |
| Key absent in Object | `"<null>"` |
| Empty `objectBytes` | `"<error>"` |
| Malformed JSON | `"<error>"` |

### 5.2 DrillDownSchemaExtractorTests

| Scenario | Expected |
|----------|----------|
| Array of objects → union of all top-level keys | Correct column names in first-seen order |
| Array of objects with varying keys → union | Missing keys produce nullable columns |
| Array with non-Object element | `Result.Failure` |
| Empty array | `Result.Failure` |
| Array of empty objects `[{}, {}]` | `Result.Failure` ("All child objects have no keys") |
| Malformed JSON | `Result.Failure` |
| `SourceFormat` on returned schema matches `format` parameter | Correct |

### 5.3 FocusedTableSourceTests

| Scenario | Expected |
|----------|----------|
| `Rows` = child count | Correct |
| `Columns` = schema.ColumnCount + 1 | Correct |
| `ColumnNames[0]` = `"#"` | Correct |
| JSON Lines: `this[row, 0]` = `"{recordPosition}:{row}"` | e.g. `"22:0"` |
| JSON Array: `this[row, 0]` = `"{recordPosition}:{row}"` | e.g. `"5:1"` |
| JSON Object: `this[row, 0]` = `"[{row}]"` | e.g. `"[0]"` |
| `this[row, n]` (n > 0) delegates to `JsonObjectCellExtractor` | Correct cell value |
| `row < 0` or `row >= Rows` | `ArgumentOutOfRangeException` |
| `col < 0` or `col >= Columns` | `ArgumentOutOfRangeException` |

---

## 6. Files to Create / Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/Json/JsonByteExtractor.cs` | Create | Shared Engine utility: `ExtractNestedBytes` (no layer dependency) |
| `src/Engine/IO/Json/JsonObjectCellExtractor.cs` | Move + rename from `src/Engine/IO/JsonLines/CellExtractor.cs` | Format-agnostic object cell extraction; namespace updated to `Refedle.Engine.IO.Json` |
| `src/Engine/IO/DrillDown/DrillDownSchemaExtractor.cs` | Create | Schema + child bytes from in-memory node |
| `src/App/Tui/DrillDownRequest.cs` | Create | DrillDown context data bag (primary constructor record) |
| `src/App/Tui/Ui/Views/FocusedTableSource.cs` | Create | `ITableSource` for FocusedTable; format-aware `#` column; bounds checking |
| `src/App/Tui/Ui/Views/FocusedTableView.cs` | Create | `MorphTableView` subclass |
| `src/App/Tui/ViewMode.cs` | Modify | Add `FocusedTable` |
| `src/App/Tui/Ui/ViewManager.cs` | Modify | Add `DrillDownAsync()` and `SwitchToFocusedTable()` (with `OnSchemaRefined = null` cleanup); update `RefreshStatusBarHints` to exclude `FocusedTable` mode and include `MorphTreeView` |
| `src/App/Tui/Ui/ModeController.cs` | Modify | Add `DrillDownAsync()` |
| `src/App/Tui/Ui/AppKeyHandler.cs` | Modify | Extend `HandleActionMenu()` for `MorphTreeView` (full branching per Section 3.8) |
| `src/App/Tui/AppState.cs` | Modify | Add `DrillDownChildValueBytes`, `DrillDownSchema`, `DrillDownFormat`, `DrillDownRecordPosition` |
| `src/App/Tui/Ui/Views/JsonObjectTreeView.cs` | Modify | Set `KeyName` on nodes created in `CreateKeyNode` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonObjectTreeNode.cs` | Modify | Add `KeyName` and `RecordPosition`; propagate in `LoadChildren` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonArrayTreeNode.cs` | Modify | Add `KeyName`, `RecordPosition`, `RawJson`; propagate in `LoadChildren` |
| `src/App/Tui/Ui/Views/JsonTreeNodes/JsonTreeNodeHelper.cs` | Modify | Accept `recordPosition`; set `KeyName`/`RecordPosition`; delegate to `JsonByteExtractor.ExtractNestedBytes` |
| `src/App/Tui/Ui/Views/JsonRangeTreeNodes/JsonLinesRangeTreeNode.cs` | Modify | Set `RecordPosition` (1-based, checked cast) on root nodes in `CreateLineNode` |
| `src/App/Tui/Ui/Views/JsonRangeTreeNodes/JsonArrayRangeTreeNode.cs` | Modify | Set `RecordPosition` (0-based, checked cast) on root nodes in `CreateElementNode` |
| Callers of `CellExtractor` in `JsonLines` sources | Modify | Update `using` / class name to `JsonObjectCellExtractor` in `Refedle.Engine.IO.Json` |
| `tests/.../JsonObjectCellExtractorTests.cs` | Rename from `CellExtractorTests.cs` | Cell extractor unit tests (rename in-place; tests unchanged) |
| `tests/.../DrillDownSchemaExtractorTests.cs` | Create | Schema extractor unit tests |
| `tests/.../FocusedTableSourceTests.cs` | Create | Table source unit tests |

---

## 7. Acceptance Criteria

- [ ] `x` key in Tree mode opens ActionMenu with DrillDown option when a `JsonArrayTreeNode` is selected
- [ ] DrillDown is disabled when selected node is not `JsonArrayTreeNode`
- [ ] DrillDown is disabled when any direct child of the selected node is not `JsonObjectTreeNode`
- [ ] Children of the selected `JsonArrayTreeNode` are displayed as rows (no file scan)
- [ ] JSON Lines: `#` column shows `{lineNumber}:{childIndex}` (e.g. `22:0`, `22:1`)
- [ ] JSON Array: `#` column shows `{elementIndex}:{childIndex}` (e.g. `5:0`, `5:1`)
- [ ] JSON Object: `#` column shows `[{childIndex}]` (e.g. `[0]`, `[1]`)
- [ ] Primitives shown as values; nested Objects as `{...}`; nested Arrays as `[...]`; absent keys as `<null>`
- [ ] `ViewMode.FocusedTable` added
- [ ] Unit tests pass for `JsonObjectCellExtractor`, `DrillDownSchemaExtractor`, `FocusedTableSource`
