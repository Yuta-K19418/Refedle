# JSON Object Explorer Mode — Design Document

## Scope

Implement Explorer Mode for JSON Object files (`{...}`), displaying top-level keys as a
tree structure. `TopLevelScanner.Scan()` provides all key-value pairs in one pass;
the App layer builds flat root-level tree nodes from those bytes — exactly mirroring the
`JsonArrayTreeView` / `JsonLinesTreeView` pattern (no wrapping root node).

---

## 1. Requirements

### Functional Requirements

- **Key Nodes at Root Level**: Each top-level key appears as a direct root-level node in the
  tree (no `{ Object }` wrapper node); matches the `JsonArrayTreeView` / `JsonLinesTreeView`
  pattern
- **Value Summary Label**: Each node's text shows `{key}: {value summary}` (e.g.
  `id: 123`, `data: {Object: 2 properties}`, `tags: [Array: 3 items]`)
- **Lazy Loading (nested)**: Child nodes of each key node are populated only when expanded;
  primitive values have no children; object/array values lazy-load their nested structure via
  existing `JsonObjectTreeNode` / `JsonArrayTreeNode`
- **Schema Preview**: Value type is inferred from the first token of the stored bytes
  (`Utf8JsonReader`); display is identical to `JsonArrayRangeTreeNode.CreateElementNode`
- **Arrow-key Navigation**: Inherited from `MorphTreeView` (Vim h/j/k/l, g/G, Ctrl+d/Ctrl+u)
- **Format Detection**: `.json` files are distinguished by peeking at the first non-whitespace
  byte: `{` → `JsonObject`, `[` → `JsonArray`

### Non-Functional Requirements

- **Zero allocations** on the tree-node rendering hot path (no allocations in `Text` after
  initial construction)
- **Native AOT compatible**: `Utf8JsonReader`; no reflection
- **Thread-safe read access**: all tree operations run on the UI thread

---

## 2. Architecture Overview

```
┌─ App Layer ──────────────────────────────────────────────────────────────┐
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │              MorphTreeView (abstract, existing)                     │ │
│  │   Vim-key navigation, 't' toggle, Enter toggle                      │ │
│  └───────────────────────────────┬─────────────────────────────────────┘ │
│                                  │                                       │
│                                  ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │              JsonObjectTreeView (new)                               │ │
│  │  - Factory method: Create(entries, onTableModeToggle)               │ │
│  │  - For each (key, valueBytes): CreateKeyNode → AddObject            │ │
│  └───────────────────────────────┬─────────────────────────────────────┘ │
│                                  │ (root-level nodes, one per key)       │
│                     ┌────────────┼────────────┐                          │
│                     ▼            ▼            ▼                          │
│  ┌──────────────────────┐  ┌──────────────┐  ┌──────────────────────┐    │
│  │ JsonValueTreeNode    │  │JsonObject    │  │ JsonArrayTreeNode    │    │
│  │ "id: 123"            │  │TreeNode      │  │ "tags: [Array: 3 …]" │    │
│  │ "name: \"example\""  │  │"data:        │  │                      │    │
│  │                      │  │{Object: 2…}" │  │                      │    │
│  └──────────────────────┘  └──────────────┘  └──────────────────────┘    │
│                    (all existing, same pattern as JsonArrayRangeTreeNode)│
│                                                                          │
├─ Engine Layer ────────────────────────────────────────────────────────── │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │              TopLevelScanner (existing)                             │ │
│  │  Scan(filePath, ct) → IReadOnlyList<(string key,                    │ │
│  │                                      ReadOnlyMemory<byte> value)>   │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Why No Root Node

`JsonArrayTreeView` and `JsonLinesTreeView` do not use a file-level wrapper node; they add
root-level nodes directly via `AddObject`. `JsonObjectTreeView` follows the same convention
to keep the tree hierarchy consistent across all formats. A wrapper root node
(`JsonObjectFileTreeNode`) was explicitly considered and rejected during design; see Section 5.

### Layer Separation

`TopLevelScanner` (Engine) returns `(string key, ReadOnlyMemory<byte> value)` pairs with no
Terminal.Gui dependency. The App layer converts those pairs into tree nodes using the same
`JsonObjectTreeNode` / `JsonArrayTreeNode` / `JsonValueTreeNode` types already used by all
other tree views. No new Engine types are required.

---

## 3. Class Design

### 3.1 App: `JsonObjectTreeView`

**Namespace**: `Refedle.App.Tui.Ui.Views`

**Responsibility**: `MorphTreeView` subclass for JSON Object files. Receives pre-scanned
key-value pairs and adds one root-level node per key via `AddObject`. Constructor is
`private`; callers use `JsonObjectTreeView.Create(...)`.

**`CreateKeyNode` pattern**: Mirrors `JsonArrayRangeTreeNode.CreateElementNode` exactly —
construct `Utf8JsonReader` on `valueBytes.Span`, read first token, create the appropriate
existing node type, and prepend `{key}: ` to its computed `Text`. This avoids calling
`JsonTreeNodeHelper.CreateChildNode`, which produces the abbreviated `{...}` / `[...]` form
used for nested child nodes.

```csharp
internal sealed class JsonObjectTreeView : MorphTreeView
{
    private JsonObjectTreeView(Action onTableModeToggle) : base(onTableModeToggle) { }

    internal static JsonObjectTreeView Create(
        IReadOnlyList<(string key, ReadOnlyMemory<byte> value)> entries,
        Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        var view = new JsonObjectTreeView(onTableModeToggle);
        foreach (var (key, valueBytes) in entries)
        {
            var node = CreateKeyNode(key, valueBytes);
            view.AddObject(node);
        }
        return view;
    }

    internal static ITreeNode CreateKeyNode(string key, ReadOnlyMemory<byte> valueBytes)
    {
        // 1. Construct Utf8JsonReader on valueBytes.Span
        // 2. Read first token (reader.Read())
        // 3. StartObject → new JsonObjectTreeNode(valueBytes); node.Text = $"{key}: {node.Text}"
        //    StartArray  → new JsonArrayTreeNode(valueBytes);  node.Text = $"{key}: {node.Text}"
        //    Primitive   → new JsonValueTreeNode($"{key}: {reader.GetPrimitiveDisplay()}")
        // 4. On JsonException or read failure → JsonValueTreeNode with "[Invalid JSON]"
    }
}
```

**No `Dispose` override required**: `JsonObjectTreeView` holds no disposable resources.
`TopLevelScanner` result is a plain list; all bytes are heap-owned `ReadOnlyMemory<byte>`.

### 3.2 App: `FormatDetector` (modified)

**Change**: Replace the `.JSON` extension branch with a content-based peek that returns
`JsonArray` or `JsonObject` based on the first non-whitespace byte.

```csharp
".JSON" => DetectJsonFormat(filePath),
```

```csharp
private static Result<DataFormat> DetectJsonFormat(string filePath)
{
    using var fs = File.OpenRead(filePath);
    int b;
    while ((b = fs.ReadByte()) != -1)
    {
        if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
        {
            break;
        }
    }
    // Explicit EOF guard: whitespace-only file (empty file is already caught upstream)
    if (b == -1)
    {
        return Results.Failure<DataFormat>("File contains no valid JSON root token");
    }
    return (char)b switch
    {
        '{' => Results.Success(DataFormat.JsonObject),
        '[' => Results.Success(DataFormat.JsonArray),
        _ => Results.Failure<DataFormat>($"Unrecognized JSON root token: '{(char)b}'"),
    };
}
```

**`t:Tree/Table` hint**: `ViewManager.RefreshStatusBarHints` shows this hint only for
`JsonLines` and `JsonArray`. `DataFormat.JsonObject` is intentionally excluded — table mode
is out of scope. No change to `RefreshStatusBarHints` is needed.

### 3.3 App: `ViewMode` (modified)

Add `JsonObjectTree` enum value.

### 3.4 App: `ViewManager` (modified)

Add `SwitchToJsonObjectTree`. Table mode is not supported for JSON Object, so `static () => { }` is passed inline as the no-op toggle callback — no dedicated method is needed.

```csharp
internal void SwitchToJsonObjectTree(
    IReadOnlyList<(string key, ReadOnlyMemory<byte> value)> entries)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentNullException.ThrowIfNull(entries);
    var view = JsonObjectTreeView.Create(entries, static () => { });
    view.X = 0;
    view.Y = 1;
    view.Width = Dim.Fill();
    view.Height = Dim.Fill() - 1;
    SwapView(view);
    RefreshStatusBarHints();
}
```

### 3.5 App: `FileDialogHandler` (modified)

**Restructuring**: Add a `stopIndexing: Action` parameter to the constructor. Add a JSON
Object branch **before** `RowIndexerFactory.Create` with an early `return`. The existing
CSV/JsonLines/JsonArray flow is unchanged.

```csharp
// After existing common reset block (_state.CurrentFilePath, ActionStack, RenewCtsWithCancel):

if (format == DataFormat.JsonObject)
{
    _stopCurrentIndexing();              // cancel previous background indexing + unwire events
    _state.RowIndexer = null;
    _state.Schema = null;
    _state.OnSchemaRefined = null;

    var ct = _state.Cts.Token;
    try
    {
        var entries = await Task.Run(() => TopLevelScanner.Scan(path, ct), ct);
        _app.Invoke(() =>
        {
            _state.CurrentMode = ViewMode.JsonObjectTree;
            _viewManager.SwitchToJsonObjectTree(entries);
        });
    }
    catch (OperationCanceledException) { /* file reloaded before scan completed */ }
    catch (Exception ex)
    {
        _app.Invoke(() => _viewManager.ShowError($"Error loading JSON Object: {ex.Message}"));
    }
    return;
}

// Unchanged: only CSV / JsonLines / JsonArray reach this line
var indexer = RowIndexerFactory.Create(format, path);
```

`_stopCurrentIndexing()` is called only in the JSON Object branch. For other formats,
`StartIndexing(indexer)` → `WireIndexerProgress` already unsubscribes from the previous
indexer, so no additional cleanup is needed.

### 3.6 App: `IndexTaskManager` (modified)

Add `CancelCurrent()` to cancel the running task without starting a new one:

```csharp
public void CancelCurrent()
{
    lock (_lock)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
```

Note: `Dispose()` is also updated to set `_cts = null` after cancellation, for symmetry with `CancelCurrent()`.

### 3.7 App: `MainWindow` (modified)

Add `StopCurrentIndexing()` method and pass it to `FileDialogHandler`:

```csharp
// Constructor change:
_fileDialogHandler = new FileDialogHandler(app, state, _viewManager, StartIndexing, StopCurrentIndexing);

// New method:
// Must be called on the UI thread; DismissIndexingProgress modifies Terminal.Gui views.
internal void StopCurrentIndexing()
{
    if (_activeIndexer is not null)
    {
        if (_onProgressChanged is not null)
            _activeIndexer.ProgressChanged -= _onProgressChanged;
        if (_onBuildIndexCompleted is not null)
            _activeIndexer.BuildIndexCompleted -= _onBuildIndexCompleted;
        _activeIndexer = null;
    }
    _indexTaskManager.CancelCurrent();
    DismissIndexingProgress();
}
```

---

## 4. Files to Create / Modify

### Files to Create

| File | Layer | Purpose |
|------|-------|---------|
| `src/App/Tui/Ui/Views/JsonObjectTreeView.cs` | App | `MorphTreeView` subclass; one root-level node per key |
| `tests/Refedle.Tests/App/Tui/Ui/Views/JsonObjectTreeViewTests.cs` | Tests | Unit tests for `JsonObjectTreeView.Create` and `CreateKeyNode` |

### Files to Modify

| File | Change |
|------|--------|
| `src/App/FormatDetector.cs` | Content-based `.json` detection; EOF guard for whitespace-only files |
| `src/App/Tui/ViewMode.cs` | Add `JsonObjectTree` |
| `src/App/Tui/Ui/ViewManager.cs` | Add `SwitchToJsonObjectTree` |
| `src/App/Tui/Ui/FileDialogHandler.cs` | Add `stopIndexing: Action` constructor param; add `JsonObject` early-return branch |
| `src/App/Tui/Workers/IndexTaskManager.cs` | Add `CancelCurrent()`; update `Dispose()` to set `_cts = null` |
| `src/App/Tui/Ui/MainWindow.cs` | Add `StopCurrentIndexing()`; pass it to `FileDialogHandler` |
| `tests/Refedle.Tests/App/Tui/Ui/FileDialogHandlerTests.cs` | Update constructor call sites for new `stopIndexing` parameter |

**Not modified**: `AppState.cs`, `RowIndexerFactory.cs` — scan results are ephemeral and
passed directly from `FileDialogHandler` to `ViewManager`; no persistent state storage needed.

---

## 5. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| No wrapper root node | `JsonArrayTreeView` and `JsonLinesTreeView` use flat root-level nodes; a wrapper node (`JsonObjectFileTreeNode`) was explicitly considered and rejected to maintain consistency |
| `CreateKeyNode` mirrors `JsonArrayRangeTreeNode.CreateElementNode` | Reuses the same node-type dispatch without calling `JsonTreeNodeHelper.CreateChildNode`, which produces abbreviated `{...}` / `[...]` labels |
| No `IRowIndexer` for JSON Object | JSON Object keys are not rows; `TopLevelScanner` is the correct data access layer — one-shot scan, then all values are in memory |
| `RowIndexerFactory.Create` not restructured | JSON Object branch returns early; the factory call is unreachable for that format without moving it |
| `stopIndexing: Action` callback in `FileDialogHandler` | `FileDialogHandler` must not own `IndexTaskManager` directly; callback keeps the dependency clean |
| `TopLevelScanner.Scan` in `Task.Run` | Synchronous file I/O; must not block the UI thread |
| `AppState` not extended for scan results | Scan results are ephemeral; JSON Object has no table-mode toggle so no re-use path exists |
| Content-based `.json` detection | Extension alone cannot distinguish JSON Object from JSON Array |
| No `ToggleJsonObjectModeAsync` method | Table mode is out of scope with no plan to add it; `static () => { }` passed inline avoids a stub method that never does anything |

---

## 6. Dependencies

| Dependency | Status | Notes |
|------------|--------|-------|
| `TopLevelScanner` | Existing | `Scan(filePath, ct)` → all key-value pairs |
| `DataFormat.JsonObject` | Existing | Already defined in `src/Engine/Types/DataFormat.cs` |
| `JsonObjectTreeNode`, `JsonArrayTreeNode`, `JsonValueTreeNode` | Existing | Reused via `CreateKeyNode` |
| `JsonArrayRangeTreeNode.CreateElementNode` | Existing | Reference implementation for `CreateKeyNode` pattern |
| `MorphTreeView` | Existing | Abstract base; Vim-key navigation |
| `System.Text.Json.Utf8JsonReader` | BCL | AOT-safe; used in `FormatDetector` and `CreateKeyNode` |
| Terminal.Gui `TreeView`, `TreeNode`, `ITreeNode` | Existing | Base for all tree classes |

---

## 7. Testing Strategy

### 7.1 Unit Tests — `JsonObjectTreeView.CreateKeyNode`

| Test | Input | Expected |
|------|-------|----------|
| `CreateKeyNode_NumberValue_ReturnsValueNodeWithLabel` | `("id", bytes of 123)` | `JsonValueTreeNode` with `Text = "id: 123"` |
| `CreateKeyNode_StringValue_ReturnsValueNodeWithLabel` | `("name", bytes of "x")` | `JsonValueTreeNode` with `Text = "name: \"x\""` |
| `CreateKeyNode_TrueValue_ReturnsValueNodeWithLabel` | `("ok", bytes of true)` | `JsonValueTreeNode` with `Text = "ok: true"` |
| `CreateKeyNode_FalseValue_ReturnsValueNodeWithLabel` | `("ok", bytes of false)` | `JsonValueTreeNode` with `Text = "ok: false"` |
| `CreateKeyNode_NullValue_ReturnsValueNodeWithLabel` | `("k", bytes of null)` | `JsonValueTreeNode` with `Text = "k: <null>"` |
| `CreateKeyNode_ObjectValue_ReturnsObjectNodeWithLabel` | `("data", bytes of {"a":1})` | `JsonObjectTreeNode` with `Text = "data: {Object: 1 properties}"` |
| `CreateKeyNode_ArrayValue_ReturnsArrayNodeWithLabel` | `("tags", bytes of [1,2])` | `JsonArrayTreeNode` with `Text = "tags: [Array: 2 items]"` |
| `CreateKeyNode_InvalidBytes_ReturnsValueNodeWithErrorText` | `("k", malformed bytes)` | `JsonValueTreeNode` with `Text = "k: [Invalid JSON]"` |
| `CreateKeyNode_EmptyBytes_ReturnsValueNodeWithErrorText` | `("k", empty)` | `JsonValueTreeNode` with `Text = "k: [Invalid JSON]"` |

### 7.2 Unit Tests — `JsonObjectTreeView.Create`

| Test | Input | Expected |
|------|-------|----------|
| `Create_WithNullEntries_ThrowsArgumentNullException` | `null` entries | `ArgumentNullException` |
| `Create_WithNullToggle_ThrowsArgumentNullException` | `null` toggle | `ArgumentNullException` |
| `Create_WithEmptyEntries_AddsNoObjects` | `[]` | 0 objects in tree |
| `Create_WithEntries_AddsOneNodePerKey` | 3 entries | 3 root-level objects |

### 7.3 Unit Tests — `FormatDetector`

| Test | Input | Expected |
|------|-------|----------|
| `Detect_JsonObjectFile_ReturnsJsonObject` | File starting with `{` | `DataFormat.JsonObject` |
| `Detect_JsonArrayFile_ReturnsJsonArray` | File starting with `[` | `DataFormat.JsonArray` |
| `Detect_JsonObjectWithLeadingWhitespace_ReturnsJsonObject` | `  \n{...}` | `DataFormat.JsonObject` |
| `Detect_JsonArrayWithLeadingWhitespace_ReturnsJsonArray` | `  \n[...]` | `DataFormat.JsonArray` |
| `Detect_JsonFileWithUnknownRoot_ReturnsFailure` | File starting with `"` | Failure result |
| `Detect_WhitespaceOnlyJsonFile_ReturnsFailure` | File with only spaces/newlines | Failure result |

### 7.4 Unit Tests — `ViewManager` (additions)

| Test | Expected |
|------|----------|
| `SwitchToJsonObjectTree_WithValidEntries_SetsCurrentView` | Current view is `JsonObjectTreeView` |
| `SwitchToJsonObjectTree_WithNullEntries_ThrowsArgumentNullException` | `ArgumentNullException` |
| `SwitchToJsonObjectTree_AfterDisposal_ThrowsObjectDisposedException` | `ObjectDisposedException` |

### 7.5 Unit Tests — `IndexTaskManager` (additions)

| Test | Expected |
|------|----------|
| `CancelCurrent_WhenIdle_DoesNotThrow` | No exception |
| `CancelCurrent_AfterStart_CancelsTask` | Background task receives cancellation |
| `CancelCurrent_CalledTwice_DoesNotThrow` | Second call with `_cts == null` is a no-op; no exception |
| `CancelCurrent_AfterDisposal_ThrowsObjectDisposedException` | `ObjectDisposedException` |
