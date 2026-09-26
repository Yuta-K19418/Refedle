# Design: Clear Action Stack Command

## Overview

Add a dedicated `c` key binding that clears all `MorphAction` entries from the `ActionStack`.
This allows users to reset all transformations without quitting or reloading the file.

## Requirements

- Pressing `c` triggers the Clear command **only when `ActionStack.Count > 0`**.
- The command shows a confirmation dialog before clearing.
- After confirmation, the `ActionStack` is reset to empty and the table view is refreshed.
- The feature is available in both `CsvTableView` and `JsonLinesTableView` (handled via the
  existing global `AppKeyHandler`).

## User Flow

1. User presses `c` in a table view.
2. `AppKeyHandler` checks `_state.ActionStack.Count`.
   - If `== 0`: keypress is ignored (no dialog shown).
   - If `> 0`: proceeds to step 3.
3. A `MessageBox.Query` confirmation dialog is shown:
   - Title: `"Clear Actions"`
   - Message: `"Clear all actions from the stack?"`
   - Buttons: `"Yes"` / `"No"`
4. If the user confirms (Yes):
   - `AppState.ClearMorphActions()` is called, resetting `ActionStack` to `[]`.
   - `ViewManager.RefreshCurrentTableView()` is called to re-render the table.
5. If the user cancels (No): nothing changes.

## Architecture

### Dispatch Flow

```
AppKeyHandler (OnGlobalKeyDown)
  └─ key == 'c'  →  HandleClearActions()
       ├─ ActionStack.Count == 0  →  return (no-op)
       ├─ MessageBox.Query(...)
       │   ├─ Yes  →  AppState.ClearMorphActions()
       │   │          ViewManager.RefreshCurrentTableView()
       │   └─ No   →  return
```

### Why a dedicated key binding instead of the `x` action menu

Clear is a stack-level operation, not a column-scoped operation. The `x` action menu
(Rename, Delete, Cast, Filter, Fill, Format Timestamp) is designed for column-specific
transformations. Placing Clear there would conflate two different levels of concern.
A dedicated `c` key keeps the action menu focused on column operations and makes the
Clear command directly accessible without navigating a menu.

## Files Changed

| File | Change |
|------|--------|
| `src/App/Tui/AppState.cs` | Add `ClearMorphActions()` method |
| `src/App/Tui/Ui/AppKeyHandler.cs` | Handle `c` key in `OnGlobalKeyDown`; add `HandleClearActions()` |
| `src/App/Tui/Ui/Views/Dialogs/HelpDialog.cs` | Document `c` key in help text |

## Out of Scope

- Per-action undo (single-step rollback) — not part of this feature.
- Persisting the cleared state to disk — clearing is in-memory only.
