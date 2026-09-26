# Design: ViewManager — Engine-to-TUI Bridge

**Issue:** #16
**Phase:** 4 — TUI Explorer (Terminal.Gui v2.0)
**References:** `docs/spec.md` §3.2, `docs/design.md` §2.3–2.4

---

## 1. Problem Statement

`MainWindow.cs` has grown into a **god class** (~350 lines) by accumulating
responsibilities that belong to a dedicated coordination layer:

| Concern currently in `MainWindow` | Correct owner |
|---|---|
| View lifecycle (create / swap / dispose child views) | `ViewManager` |
| Engine object construction (`DataRowIndexer`, `RowIndexer`, `IncrementalSchemaScanner`) | `FileLoader` |
| Background task orchestration for schema refinement | `FileLoader` |
| Error display | `ViewManager` |
| Menu bar and status bar initialization | `MainWindow` (keep) |
| Keyboard shortcut wiring | `MainWindow` (keep) |

This violates the **Single Responsibility Principle** and makes `MainWindow`
hard to test and extend.

### Why a separate `ViewManager` and `FileLoader`?

- `spec.md §3.2` and `design.md §2.3` describe a "Virtualized View Manager" as
  a first-class architectural component.
- `MainWindow` should own only the chrome (menu/status bar) and delegate all
  content management to `ViewManager`.
- Engine object construction and background tasks belong to `FileLoader`, which
  knows the Engine layer but nothing about Terminal.Gui.
- `ViewManager` knows only the TUI layer; it receives ready-made Engine objects
  and switches views accordingly.
- Isolating view-switching logic makes future view additions (recipe editor,
  filter panel, etc.) a matter of adding a method to `ViewManager` rather than
  modifying `MainWindow`.

---

## 2. Goals

1. Extract all content-view management out of `MainWindow` into a new
   `ViewManager` class.
2. Extract Engine object construction and background tasks into a new
   `FileLoader` class.
3. `FileLoader` acts as the bridge between raw file paths and the Engine layer,
   updating `AppState` without any TUI dependency.
4. `ViewManager` acts as the bridge between Engine-layer objects (held in
   `AppState`) and Terminal.Gui views.
5. Integrate `LazyTransformer` transparently so every table view passes data
   through the Action Stack.
6. `MainWindow` is reduced to ≤ 120 lines: menu, status bar, and thin
   delegation calls to `FileLoader` and `ViewManager`.
7. Zero regressions: all existing paths (CSV, JSONL tree, JSONL table,
   placeholder, error) must continue to work.

---

## 3. Architecture

```
┌─────────────────────────── MainWindow (Window) ──────────────────────────┐
│  MenuBar  ·  StatusBar                                                   │
│                                                                          │
│  OnOpenFile()                                                            │
│    ├─► FileLoader.LoadAsync(path)   ── updates AppState ──►              │
│    └─► ViewManager.SwitchTo*(...)   ── reads AppState   ──►              │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐   │
│  │                        FileLoader                                 │   │
│  │                                                                   │   │
│  │  LoadAsync(path)                                                  │   │
│  │   ├─ DetectFormat                                                 │   │
│  │   ├─ CSV  → LoadCsvAsync()                                        │   │
│  │   │           ├─ new DataRowIndexer                               │   │
│  │   │           ├─ new IncrementalSchemaScanner                     │   │
│  │   │           ├─ fire-and-forget BuildIndex                       │   │
│  │   │           └─ AppState.{Indexer, Schema, Mode} = ...           │   │
│  │   └─ JSONL → LoadJsonLinesAsync()                                 │   │
│  │               ├─ new RowIndexer                                   │   │
│  │               ├─ fire-and-forget BuildIndex                       │   │
│  │               └─ AppState.{JsonLinesIndexer, Mode} = ...          │   │
│  │                                                                   │   │
│  │  ToggleJsonLinesModeAsync()  (schema scan logic)                  │   │
│  └───────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐   │
│  │                        ViewManager                                │   │
│  │                                                                   │   │
│  │  SwitchToCsvTable(indexer, schema)                                │   │
│  │  SwitchToJsonLinesTree(indexer)                                   │   │
│  │  SwitchToJsonLinesTableView(indexer, schema)                      │   │
│  │  SwitchToFileSelection()                                          │   │
│  │  ShowError(message)                                               │   │
│  │                                                                   │   │
│  │  _container : Window  ◄── Window reference                        │   │
│  │  _currentView : View?                                             │   │
│  └───────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘

Engine Layer                         TUI Layer
─────────────────────────────        ──────────────────────────────────────
DataRowIndexer                  →    VirtualTableSource
  └─ TableSchema                     └─(wrapped by LazyTransformer)
                                          └─ CsvTableView

RowIndexer                      →    JsonLinesTreeView
  └─ (schema on demand)              ToggleMode → JsonLinesTableSource
                                          └─(wrapped by LazyTransformer)
                                               └─ JsonLinesTableView
```

Dependency direction:

```
MainWindow ──► FileLoader  (knows Engine + AppState; no TUI)
           └─► ViewManager (knows TUI + AppState; no Engine)
```

`FileLoader` and `ViewManager` do **not** depend on each other.

---

## 4. Component Design

### 4.1 `FileLoader`

**File:** `src/App/FileLoader.cs`

```csharp
namespace Refedle.App;

internal sealed class FileLoader : IDisposable
{
    FileLoader(AppState state)

    // Called by MainWindow after a file is chosen
    Task LoadAsync(string filePath);

    // Called by MainWindow when the user toggles JSON-Lines display mode
    Task ToggleJsonLinesModeAsync();

    // IDisposable — cancels background tasks
    void Dispose();
}
```

**Fields:**

| Field | Type | Purpose |
|---|---|---|
| `_state` | `AppState` | Shared application state; updated after each load |

#### 4.1.1 `LoadAsync`

```
LoadAsync(path)
  ├─ path ends with ".csv"   → LoadCsvAsync(path)
  ├─ path ends with ".jsonl" → LoadJsonLinesAsync(path)
  └─ otherwise               → sets _state.LastError = "Unsupported format"
```

**`LoadCsvAsync`:**
1. Construct `DataRowIndexer`; fire-and-forget `BuildIndex` on `Task.Run`.
2. Construct `IncrementalSchemaScanner`; call `InitialScanAsync()`.
3. Store indexer, scanner, and initial schema in `_state`.
4. Set `_state.CurrentMode = ViewMode.CsvTable`.
5. Start background schema refinement; on completion call `_state.Schema = refined`.

**`LoadJsonLinesAsync`:**
1. Construct `RowIndexer`; fire-and-forget `BuildIndex` on `Task.Run`.
2. Store indexer in `_state` (schema is `null` until table mode).
3. Set `_state.CurrentMode = ViewMode.JsonLinesTree`.

#### 4.1.2 `ToggleJsonLinesModeAsync`

Mirrors the existing `HandleTableModeToggleAsync` logic in `MainWindow`, moved
to `FileLoader`:

```
CurrentMode == JsonLinesTable  → _state.CurrentMode = JsonLinesTree
CurrentMode == JsonLinesTree   →
  schema cached?  → _state.CurrentMode = JsonLinesTable
  otherwise       → scan schema lazily, cache in _state, set JsonLinesTable
```

---

### 4.2 `ViewManager`

**File:** `src/App/Tui/Ui/ViewManager.cs`

```csharp
namespace Refedle.App.Tui.Ui;

internal sealed class ViewManager : IDisposable
{
    ViewManager(Window container, AppState state)

    // Called by MainWindow to switch the visible content view
    void SwitchToFileSelection();
    void SwitchToCsvTable(DataRowIndexer indexer, TableSchema schema);
    void SwitchToJsonLinesTree(RowIndexer indexer);
    void SwitchToJsonLinesTableView(RowIndexer indexer, TableSchema schema);
    void ShowError(string message);

    // IDisposable — disposes the current content view
    void Dispose();
}
```

**Fields:**

| Field | Type | Purpose |
|---|---|---|
| `_container` | `Window` | Target Terminal.Gui container for child views |
| `_state` | `AppState` | Shared application state |
| `_currentView` | `View?` | Active content view; replaced on every switch |

**View-switching helpers (private):**

| Method | Creates |
|---|---|
| `SwapView(View newView)` | Removes old, disposes old, adds new |

#### 4.2.1 `LazyTransformer` Integration

Every `SwitchToCsvTable` and `SwitchToJsonLinesTableView` wraps the raw
`ITableSource` with `LazyTransformer` before assigning to the view:

```csharp
// Inside SwitchToCsvTable
var rawSource = new VirtualTableSource(indexer, schema);
ITableSource source = _state.ActionStack.Count == 0
    ? rawSource
    : new LazyTransformer(rawSource, schema, _state.ActionStack);

var view = new CsvTableView { Table = source, ... };
SwapView(view);
```

When `ActionStack` is empty, `LazyTransformer` is skipped (pure passthrough,
no allocation overhead).

#### 4.2.2 `SwapView`

```csharp
private void SwapView(View newView)
{
    if (_currentView is not null)
    {
        _container.Remove(_currentView);
        _currentView.Dispose();
    }
    _currentView = newView;
    _container.Add(_currentView);
}
```

All `SuppressMessage("CA2000")` justifications currently scattered across
`MainWindow` are consolidated here — child views are owned by the container
after `Add()`.

---

### 4.3 Revised `MainWindow`

After extraction, `MainWindow` retains only:

- Constructor: initialize menu bar, status bar, create `FileLoader` and
  `ViewManager`, show initial `FileSelectionView`.
- `InitializeMenu()` / `InitializeStatusBar()`: unchanged.
- `ShowFileDialogAsync()`: show dialog, call `_fileLoader.LoadAsync(path)`,
  then call the appropriate `_viewManager.SwitchTo*(...)`.
- `HandleToggleAsync()`: call `_fileLoader.ToggleJsonLinesModeAsync()`,
  then call the appropriate `_viewManager.SwitchTo*(...)`.
- `Dispose()`: dispose `_fileLoader` and `_viewManager`.

Target size: ≤ 120 lines.

---

## 5. Files Changed / Created

| Action | File |
|---|---|
| **Create** | `src/App/FileLoader.cs` |
| **Create** | `src/App/Tui/Ui/ViewManager.cs` |
| **Modify** | `src/App/Tui/Ui/MainWindow.cs` (extract to `FileLoader`/`ViewManager`, delegate calls) |
| **No change** | `src/App/Tui/AppState.cs` |
| **No change** | `src/App/Tui/ViewMode.cs` |
| **No change** | `src/App/Tui/Ui/Views/` (all existing views unchanged) |
| **No change** | `src/Engine/` (all engine classes unchanged) |

---

## 6. AppState Compatibility

`AppState` already holds all the fields needed by both `FileLoader` and
`ViewManager`:

```csharp
public string CurrentFilePath { get; set; }
public ViewMode CurrentMode { get; set; }
public TableSchema? Schema { get; set; }
public IncrementalSchemaScanner? SchemaScanner { get; set; }
public CancellationTokenSource Cts { get; set; }
public RowIndexer? JsonLinesIndexer { get; set; }
public JsonLinesSchema.IncrementalSchemaScanner? JsonLinesSchemaScanner { get; set; }
public IReadOnlyList<MorphAction> ActionStack { get; set; }
```

No changes to `AppState` are required for this issue.

---

## 7. Threading Model

- Both `FileLoader` and `ViewManager` run on the **Terminal.Gui UI thread**
  (same as `MainWindow`).
- Background tasks (`BuildIndex`, `StartBackgroundScanAsync`) are dispatched
  with `Task.Run` and post results back to `_state`; this is identical to the
  existing pattern in `MainWindow`.
- Future issues introducing thread-safety to `AppState` are out of scope here.

---

## 8. Error Handling

`ShowError(string message)` in `ViewManager` creates a `PlaceholderView` and
sets its `Text` to the error message, identical to the current
`MainWindow.ShowError`.

`FileLoader.LoadCsvAsync` catches `ArgumentException` and `IOException`;
sets `_state.LastError` and the caller (`MainWindow`) calls
`_viewManager.ShowError(...)`. `ToggleJsonLinesModeAsync` catches
`InvalidOperationException`. All other exceptions propagate to Terminal.Gui's
built-in handler.

---

## 9. Testing Strategy

### Unit Tests

**New file:** `tests/Refedle.Tests/App/FileLoaderTests.cs`

| Test | Validates |
|---|---|
| `LoadCsvAsync_UpdatesStateAndMode` | After a CSV load, `CurrentMode == CsvTable`, `Schema` is set |
| `LoadJsonLinesAsync_UpdatesStateAndMode` | After JSONL load, `CurrentMode == JsonLinesTree`, `JsonLinesIndexer` is set |
| `ToggleJsonLinesMode_TreeToTable_ScansSchema` | Switching from Tree triggers schema scan, mode becomes `JsonLinesTable` |
| `ToggleJsonLinesMode_TableToTree_RestoresTree` | Switching back sets mode to `JsonLinesTree` |
| `LoadUnsupportedFormat_SetsLastError` | Unsupported extension sets `_state.LastError`, mode unchanged |

**New file:** `tests/Refedle.Tests/App/Tui/Ui/ViewManagerTests.cs`

Because Terminal.Gui views cannot be instantiated headlessly, `ViewManager`
tests are limited to integration/manual verification. Unit test focus is on
`FileLoader` state transitions.

Testing infrastructure:
- `xUnit` + `AwesomeAssertions` (existing project dependencies).
- Terminal.Gui components (`Window`, `View`) are not instantiated in unit tests.

### Manual Regression Checklist

1. Launch app → `FileSelectionView` displayed.
2. Open CSV → `CsvTableView` renders with virtualized data.
3. Open JSONL → `JsonLinesTreeView` renders records.
4. Press `T` in JSONL tree → `JsonLinesTableView` renders.
5. Press `T` again → returns to tree.
6. Open unsupported `.json` → error or placeholder shown.
7. Quit with `Ctrl+X` → clean shutdown, no exceptions.

---

## 10. Success Criteria

- `MainWindow.cs` is ≤ 120 lines with no `SwitchTo*` or `Load*` methods.
- `FileLoader.cs` is ≤ 200 lines (SRP compliance per project standards).
- `ViewManager.cs` is ≤ 200 lines (SRP compliance per project standards).
- `dotnet build` produces **zero warnings**.
- `dotnet test` passes with **zero failures**.
- `dotnet format` reports no changes.
- All manual regression checklist items pass on macOS Terminal.
