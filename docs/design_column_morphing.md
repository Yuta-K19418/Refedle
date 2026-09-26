# Design: Interactive Column Morphing (Rename, Delete, Cast)

**Phase:** 4 — TUI Explorer (Terminal.Gui v2.0)
**References:** `docs/spec.md` §3.2, `docs/design.md` §2.4

---

## 1. Requirements

From `spec.md §3.2`:

> **Key-Driven Morphing**: Delete columns (`D`), Rename (`R`), and Cast types (`C`)
> with instant visual feedback.

The user must be able to perform these actions interactively while viewing a CSV
or JSON Lines table, with the results reflected immediately in the current view.

---

## 2. Architecture Overview

The Action Stack infrastructure (`MorphAction` subtypes, `AppState.ActionStack`,
`LazyTransformer`) is fully implemented. This feature adds the **interactive TUI
layer** that lets users build that stack.

```
User presses R / D / C on a focused column
            ↓
  CsvTableView / JsonLinesTableView.OnKeyDown()
            ↓
  Dialogs (RenameColumnDialog / DeleteColumnDialog / CastColumnDialog)
    ← user fills in dialog, confirms →
            ↓
  AppState.AddMorphAction(action)       ← ActionStack extended
            ↓
  ViewManager.RefreshCurrentTableView() ← SwitchTo* called again
            ↓
  LazyTransformer reconstructed with new ActionStack
            ↓
  TableView re-renders with instant visual feedback
```

### Dependency direction (unchanged)

```
MainWindow ──► FileLoader  (Engine + AppState; no TUI)
           └─► ViewManager (TUI + AppState; no Engine)
```

Dialogs live entirely in the TUI layer. No Engine changes are required.

---

## 3. Key Bindings

| Key | Action |
|-----|--------|
| `Shift+R` (uppercase R) | Rename focused column |
| `Shift+D` (uppercase D) | Delete focused column |
| `Shift+C` (uppercase C) | Cast focused column type |

Uppercase letters are used per `spec.md §3.2`. They do not conflict with the
existing `VimKeyTranslator` (which handles only `h/j/k/l/g/G`).

Both `CsvTableView` and `JsonLinesTableView` handle these keys before delegating
to `VimKeyTranslator`.

---

## 4. Files Changed / Created

| Action | File |
|--------|------|
| **Create** | `src/App/Tui/Ui/Views/Dialogs/RenameColumnDialog.cs` |
| **Create** | `src/App/Tui/Ui/Views/Dialogs/DeleteColumnDialog.cs` |
| **Create** | `src/App/Tui/Ui/Views/Dialogs/CastColumnDialog.cs` |
| **Modify** | `src/App/Tui/AppState.cs` |
| **Modify** | `src/App/Tui/Ui/ViewManager.cs` |
| **Modify** | `src/App/Tui/Ui/Views/CsvTableView.cs` |
| **Modify** | `src/App/Tui/Ui/Views/JsonLinesTableView.cs` |
| **Create** | `tests/Refedle.Tests/App/Tui/AppStateTests.cs` |

No changes to `FileLoader`, `MainWindow`, `LazyTransformer`, or any Engine class.

---

## 5. Component Design

### 5.1 Dialog Classes

All three dialogs inherit from Terminal.Gui's `Dialog`.
They are shown via `Application.Run(dialog)` from within `OnKeyDown`, which
is the standard Terminal.Gui v2 pattern for modal dialogs.

#### 5.1.1 `RenameColumnDialog`

**File:** `src/App/Tui/Ui/Views/Dialogs/RenameColumnDialog.cs`

```
RenameColumnDialog(currentName: string)

Layout:
  ┌─ Rename Column ──────────────────┐
  │ Current: {currentName}           │
  │ New name: [__________________]   │
  │              [ OK ] [ Cancel ]   │
  └──────────────────────────────────┘

Properties:
  NewName   : string?   ← null if cancelled; the validated name if confirmed
  Confirmed : bool
```

- The OK button is disabled while `TextField` is empty.
- Pressing `Enter` in the text field triggers OK (if not empty).
- If the user enters the same name as `currentName`, `Confirmed` is `false`
  (no-op; no action is added to the stack).

#### 5.1.2 `DeleteColumnDialog`

**File:** `src/App/Tui/Ui/Views/Dialogs/DeleteColumnDialog.cs`

```
DeleteColumnDialog(columnName: string)

Layout:
  ┌─ Delete Column ──────────────────┐
  │ Delete column '{columnName}'?    │
  │            [ Yes ] [ No ]        │
  └──────────────────────────────────┘

Properties:
  Confirmed : bool
```

#### 5.1.3 `CastColumnDialog`

**File:** `src/App/Tui/Ui/Views/Dialogs/CastColumnDialog.cs`

```
CastColumnDialog(columnName: string, currentType: ColumnType)

Layout:
  ┌─ Cast Column Type ───────────────┐
  │ Column: {columnName}             │
  │ Current type: {currentType}      │
  │ ◉ Text                           │
  │ ○ WholeNumber                    │
  │ ○ FloatingPoint                  │
  │ ○ Boolean                        │
  │ ○ Timestamp                      │
  │ ○ JsonObject                     │
  │ ○ JsonArray                      │
  │              [ OK ] [ Cancel ]   │
  └──────────────────────────────────┘

Properties:
  SelectedType : ColumnType?   ← null if cancelled
  Confirmed    : bool
```

- RadioGroup pre-selects `currentType` on open.
- If the user selects the same type as `currentType`, `Confirmed` is `false`
  (no-op; no action is added to the stack).

---

### 5.2 `AppState` — `AddMorphAction`

**File:** `src/App/Tui/AppState.cs`

Add one method:

```csharp
internal void AddMorphAction(MorphAction action)
{
    ActionStack = [..ActionStack, action];
}
```

- Uses collection expression spread to create a new `IReadOnlyList<MorphAction>`.
- Preserves immutability: the previous list is never mutated.
- Called on the UI thread only; no synchronization required.

---

### 5.3 `ViewManager` — `RefreshCurrentTableView`

**File:** `src/App/Tui/Ui/ViewManager.cs`

Add one internal method and update both `SwitchToCsvTable` and
`SwitchToJsonLinesTableView` to wire up the morph callback.

#### `RefreshCurrentTableView`

```csharp
internal void RefreshCurrentTableView()
{
    switch (_state.CurrentMode)
    {
        case ViewMode.CsvTable
            when _state.CsvIndexer is not null && _state.Schema is not null:
            SwitchToCsvTable(_state.CsvIndexer, _state.Schema);
            break;

        case ViewMode.JsonLinesTable
            when _state.JsonLinesIndexer is not null && _state.Schema is not null:
            SwitchToJsonLinesTableView(_state.JsonLinesIndexer, _state.Schema);
            break;
    }
}
```

- Reads the current indexer and schema directly from `AppState` (already stored
  there from the `FileLoader` load step).
- Re-invokes the same `SwitchTo*` method, which reconstructs `LazyTransformer`
  with the now-updated `ActionStack`.

#### Morph callback in `SwitchToCsvTable`

```csharp
var onMorphAction = (MorphAction action) =>
{
    _state.AddMorphAction(action);
    RefreshCurrentTableView();
};

var view = new Views.CsvTableView
{
    ...,
    OnMorphAction = onMorphAction,
};
```

The same pattern is applied in `SwitchToJsonLinesTableView`, passing the
callback to `JsonLinesTableView`'s constructor.

---

### 5.4 `CsvTableView` — column morph keys

**File:** `src/App/Tui/Ui/Views/CsvTableView.cs`

Add `OnMorphAction` property and morph key handling:

```csharp
/// <summary>
/// Callback invoked when the user confirms a column morphing action.
/// Null means morphing is disabled for this view instance.
/// </summary>
internal Action<MorphAction>? OnMorphAction { get; init; }
```

In `OnKeyDown`, before the vim-key translation:

```csharp
// Shift+R → Rename
if (key.KeyCode == (KeyCode.R | KeyCode.ShiftMask))
    return HandleRenameColumn();

// Shift+D → Delete
if (key.KeyCode == (KeyCode.D | KeyCode.ShiftMask))
    return HandleDeleteColumn();

// Shift+C → Cast
if (key.KeyCode == (KeyCode.C | KeyCode.ShiftMask))
    return HandleCastColumn();
```

Helper methods (private, called only when a column is focused):

```csharp
private bool HandleRenameColumn()
{
    if (OnMorphAction is null || Table is null || SelectedColumn < 0)
        return false;

    var columnName = Table.ColumnNames[SelectedColumn];
    using var dialog = new Dialogs.RenameColumnDialog(columnName);
    Application.Run(dialog);

    if (!dialog.Confirmed || dialog.NewName is null)
        return true;  // key consumed; dialog cancelled

    OnMorphAction(new RenameColumnAction { OldName = columnName, NewName = dialog.NewName });
    return true;
}
```

`HandleDeleteColumn` and `HandleCastColumn` follow the same pattern with their
respective dialog types. All three return `true` to consume the key regardless
of whether the dialog was confirmed or cancelled.

**Guard condition for `HandleCastColumn`:** The `LazyTransformer` applies cast
formatting lazily. To determine the *current* `ColumnType` of the focused column
(after any prior renames/casts), `CsvTableView` must resolve the type from the
active `ITableSource`. Since `ITableSource` does not expose column types, the
dialog opens with `ColumnType.Text` as the pre-selected default when the type
cannot be resolved from the source. This is a known limitation of the MVP.

---

### 5.5 `JsonLinesTableView` — column morph keys

**File:** `src/App/Tui/Ui/Views/JsonLinesTableView.cs`

Add `onMorphAction` as a second constructor parameter (nullable, default `null`):

```csharp
internal JsonLinesTableView(Action onTableModeToggle, Action<MorphAction>? onMorphAction = null)
```

Key handling is identical to `CsvTableView`'s helper methods. The three
`HandleRenameColumn` / `HandleDeleteColumn` / `HandleCastColumn` helpers are
duplicated rather than extracted to a shared utility class, to keep each view
self-contained and avoid coupling. (Total duplication is ≈ 30 lines.)

---

## 6. `ITableSource` — Column Type Limitation

`ITableSource` (Terminal.Gui) exposes only `ColumnNames`, `Rows`, `Columns`,
and `this[row, col]`. Column types are not exposed through this interface.

The `CastColumnDialog` therefore cannot pre-select the current type automatically.
The dialog will open with `ColumnType.Text` pre-selected.

This is consistent with the existing `LazyTransformer` design, where types are
tracked in the transformation pipeline only — not surfaced back to the view.
A future improvement could expose a typed column metadata interface.

---

## 7. Data Flow Example

**Scenario:** User opens a CSV, sees columns `[name, age, score]`. User renames
`score` to `points`, then deletes `age`.

```
Initial state: ActionStack = []
Table shows:   [name, age, score]

Step 1 — cursor on column "score", press Shift+R
  → RenameColumnDialog shown, user types "points", confirms
  → AppState.AddMorphAction(RenameColumnAction { OldName="score", NewName="points" })
  → ActionStack = [RenameColumnAction { OldName="score", NewName="points" }]
  → ViewManager.RefreshCurrentTableView()
  → SwitchToCsvTable called
  → LazyTransformer(source, schema, ActionStack) constructed
  → Table shows: [name, age, points]

Step 2 — cursor on column "age", press Shift+D
  → DeleteColumnDialog shown, user confirms
  → AppState.AddMorphAction(DeleteColumnAction { ColumnName="age" })
  → ActionStack = [Rename(score→points), Delete(age)]
  → ViewManager.RefreshCurrentTableView()
  → New LazyTransformer constructed
  → Table shows: [name, points]
```

---

## 8. Testing Strategy

### Unit Tests

**New file:** `tests/Refedle.Tests/App/Tui/AppStateTests.cs`

| Test | Validates |
|------|-----------|
| `AddMorphAction_SingleAction_AddsToStack` | After one call, `ActionStack.Count == 1` |
| `AddMorphAction_MultipleActions_PreservesOrder` | Three actions added in order; stack reflects insertion order |
| `AddMorphAction_DoesNotMutateOriginalList` | Original `IReadOnlyList` reference is replaced, not mutated |

### Manual Regression Checklist

1. Open a CSV file → `CsvTableView` renders.
2. Focus a column → press `Shift+R` → `RenameColumnDialog` opens.
3. Enter a new name → confirm → column header updates immediately.
4. Focus a column → press `Shift+D` → `DeleteColumnDialog` opens.
5. Confirm → column disappears from the table.
6. Focus a column → press `Shift+C` → `CastColumnDialog` opens.
7. Select a type → confirm → cells in that column reformat (e.g., `<invalid>` for non-parseable values).
8. Cancel any dialog → no change to the table.
9. Repeat all steps 2–8 for a JSON Lines file in table mode.
10. Open a second file → `ActionStack` resets to `[]` (no stale transformations).
11. Quit with `Ctrl+X` → clean shutdown.

> **Note:** Item 10 (ActionStack reset on file load) requires verifying that
> `FileLoader.LoadAsync` resets `AppState.ActionStack = []` before loading the
> new file. This behaviour is added as part of this implementation.

---

## 9. Threading Model

All dialog interactions and `AppState.AddMorphAction` calls occur on the
Terminal.Gui UI thread. `Application.Run(dialog)` blocks the UI thread until
the dialog is dismissed (standard Terminal.Gui modal behaviour). No additional
synchronization is required.

---

## 10. Error Handling

- If no column is focused when a morph key is pressed, the key is consumed but
  no dialog is shown (silent no-op).
- If the user cancels a dialog, no action is added to the stack.
- If `Table` is `null` when a morph key is pressed, the key is consumed but no
  dialog is shown.

---

## 11. Success Criteria

- `dotnet build` produces **zero warnings**.
- `dotnet test` passes with **zero failures**.
- `dotnet format` reports no changes.
- `ViewManager.cs` remains ≤ 300 lines (SRP compliance).
- Each dialog class is ≤ 100 lines.
- All manual regression checklist items pass.
