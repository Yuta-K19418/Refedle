# DrillDown Backspace Navigation — Design Document

## Scope

After a DrillDown (Single or Full Aggregation) switches the app to `ViewMode.FocusedTable`, there
is currently no way back to the tree view that triggered it short of reopening the file. This adds
`Backspace` in `FocusedTable` mode to return to the originating tree mode.

**In scope:** single-level back navigation — `FocusedTable` → the one tree mode (`JsonLinesTree`,
`JsonArrayTree`, or `JsonObjectTree`) it was entered from.

**Out of scope:**
- Multi-level DrillDown history/stack (explicitly excluded by the issue).
- Restoring the exact tree cursor position / scroll offset / expanded-node state the user left
  behind (see Design §2 and ADR 2 for why).

## 1. Existing Infrastructure (Reused)

- `AppState.CurrentMode` / `ViewMode` — `src/App/Tui/AppState.cs`, `src/App/Tui/ViewMode.cs`. DrillDown
  entry points already read `_state.CurrentMode` immediately before overwriting it with
  `ViewMode.FocusedTable` (`ModeController.DrillDown`, `ViewManager.FullAggregationDrillDownAsync`).
- `AppState.RowIndexer` — already persisted across mode switches (this is what lets `t` toggle
  JSON Lines Tree ⇄ Table without re-scanning). Reusable as-is for returning to `JsonLinesTree` /
  `JsonArrayTree`.
- `ViewManager.SwitchToJsonLinesTree(indexer)` / `SwitchToJsonArrayTree(indexer)` /
  `SwitchToJsonObjectTree(entries)` — each rebuilds the tree view from scratch and resets the
  breadcrumb to `[]` (root). Reused unchanged as the "go back" mechanism.

## 2. Gap: Tree Views Are Always Rebuilt From Scratch

Every `SwitchTo*Tree` method calls `UpdateBreadcrumb([], collapseIndices: false)` and constructs a
brand-new `TreeView`, discarding whatever selection/expansion state the previous instance had (this
is also true today for the `t`-key Table→Tree toggle — going back to Tree already resets to root).
There is no existing "select node by KeyPath" capability anywhere in the tree view layer.

Given that, returning from `FocusedTable` via `Backspace` lands on the tree at its root, same as
every other tree (re)construction path in the app today. See ADR 2.

## 3. Gap: `JsonObjectTree` Has No Persisted Backing Data

`JsonLinesTree`/`JsonArrayTree` can be rebuilt from `AppState.RowIndexer`, which is cached at file
load and never cleared while that file stays open. `JsonObjectTree`, however, is built from
`entries` (`IReadOnlyList<(string key, JsonRawBytes value)>`) returned by
`Engine.IO.JsonObject.TopLevelScanner.Scan` — computed once in
`FileDialogHandler.HandleFileSelectedAsync` and passed directly to
`ViewManager.SwitchToJsonObjectTree`, never stored anywhere. Without caching it, returning to
`JsonObjectTree` would require re-running `TopLevelScanner.Scan` against the file (an extra async
file read) purely to reconstruct a view the app already built once this session.

### 3.1 Element Type: `JsonObjectEntry`, Not a Bare `ValueTuple`

`TopLevelScanner.Scan`'s existing return type, `IReadOnlyList<(string key, JsonRawBytes value)>`,
is a reasonable shape for a value passed once from a scan to its single caller
(`SwitchToJsonObjectTree`). Caching it on `AppState`, however, promotes it to a long-lived,
app-wide domain concept — the same promotion `RowIndexer` already went through — and an unnamed,
transient tuple shape is not an appropriate type for that role.

**Decision:** introduce a dedicated `readonly record struct JsonObjectEntry(string Key, JsonRawBytes Value)`
in `Engine.IO.JsonObject` (alongside `TopLevelScanner`, same layer) and use it everywhere this shape
flows, not just on `AppState`:

```csharp
/// <summary>A single top-level key/value pair from a JSON Object file, as scanned by <see cref="TopLevelScanner"/>.</summary>
public readonly record struct JsonObjectEntry(string Key, JsonRawBytes Value);
```

`record struct` (not a `class`), and `readonly`, briefly: (1) `JsonObjectEntry` is a `struct`, not a
`class`, keeping the same inline-storage characteristics the existing `ValueTuple` shape already
has — making it a `class` instead would move each cached entry to the heap individually, adding
GC-tracked allocations that don't exist today; (2) per the project's immutability standard
(`.claude/rules/csharp-standards.md`: "Mutable fields and mutable properties require
justification"), state living on `AppState` should be immutable by default, and there is no
justification for mutability here.

- `TopLevelScanner.Scan` returns `IReadOnlyList<JsonObjectEntry>` instead of the bare tuple.
- `JsonObjectTreeView` and `ViewManager.SwitchToJsonObjectTree` take `IReadOnlyList<JsonObjectEntry>`.
- `AppState.JsonObjectEntries` is `IReadOnlyList<JsonObjectEntry>?`, cached the same way `RowIndexer` is:

```csharp
/// <summary>
/// Gets or sets the cached top-level entries for JSON Object tree reconstruction.
/// Set once at file load for <see cref="DataFormat.JsonObject"/> files; null for all other formats.
/// </summary>
public IReadOnlyList<JsonObjectEntry>? JsonObjectEntries { get; set; }
```

`FileDialogHandler.HandleFileSelectedAsync` resets it to `null` in the existing "Reset state for
new file" block (alongside `_state.DrillDown = null;`), and sets it in the existing
`DataFormat.JsonObject` branch right where `_state.CurrentMode = ViewMode.JsonObjectTree;` is set
today.

Changing the existing `Scan` signature (rather than introducing the struct only for the new
`AppState` field and converting at the boundary) keeps exactly one shape for this data instead of
two, and avoids allocating a converted copy purely to satisfy a type mismatch. This widens the
touched-file set beyond what Backspace navigation alone would require (see §8).

**Alternative considered and rejected:** re-run `TopLevelScanner.Scan` on `Backspace` instead of
caching, avoiding the extra `AppState` field entirely. Rejected — caching costs one small in-memory
list for the lifetime of an already-open file (the same tradeoff `RowIndexer` already makes), while
re-scanning would add file I/O latency and async error handling to `Backspace` that every other
`ReturnFromDrillDown` branch doesn't need (`JsonLinesTree`/`JsonArrayTree` are synchronous, reusing
the already-cached `RowIndexer`). Caching keeps all three `PreviousMode` branches symmetric.

## 4. Design: `DrillDownState.PreviousMode`

`DrillDownState` gains one field recording which tree mode was active immediately before the
switch to `FocusedTable`:

```csharp
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema,
    ViewMode PreviousMode);
```

Populated at both DrillDown entry points, reading `_state.CurrentMode` before it is overwritten:

- `ModeController.DrillDown(SingleDrillDownRequest)` — capture `_state.CurrentMode` right before
  constructing `DrillDownState` / setting `CurrentMode = ViewMode.FocusedTable`.
- `ModeController.FullAggregationDrillDownAsync(FullAggregationDrillDownRequest)` — capture
  alongside the existing `filePath`/`ct` capture-before-`Task.Run` (same rationale already
  documented inline: reading `_state` off the calling thread, not the thread-pool thread).
  `CurrentMode` isn't set to `FocusedTable` inside this method today — that happens in
  `ViewManager.FullAggregationDrillDownAsync` after the await — so it must be captured here, before
  the background scan starts, while it's still the tree mode.

`PreviousMode` lives and dies with the `DrillDownState` it's attached to, so it can never get out
of sync with the DrillDown session it describes (see ADR 1).

## 5. Design: Returning From `FocusedTable`

New `ViewManager.ReturnFromDrillDown()`:

```csharp
internal void ReturnFromDrillDown()
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_state.DrillDown is not { } drillDown)
    {
        return;
    }

    _state.DrillDown = null;

    switch (drillDown.PreviousMode)
    {
        case ViewMode.JsonLinesTree when _state.RowIndexer is not null:
            _state.CurrentMode = ViewMode.JsonLinesTree;
            SwitchToJsonLinesTree(_state.RowIndexer);
            break;

        case ViewMode.JsonArrayTree when _state.RowIndexer is not null:
            _state.CurrentMode = ViewMode.JsonArrayTree;
            SwitchToJsonArrayTree(_state.RowIndexer);
            break;

        case ViewMode.JsonObjectTree when _state.JsonObjectEntries is not null:
            _state.CurrentMode = ViewMode.JsonObjectTree;
            SwitchToJsonObjectTree(_state.JsonObjectEntries);
            break;

        default:
            throw new UnreachableException(
                "DrillDownState.PreviousMode must be a tree mode with its backing data still cached on AppState.");
    }
}
```

`AppKeyHandler` wires `Backspace` the same way as the existing single-key shortcuts:

```csharp
private bool HandleDrillDownBack()
{
    if (_state.CurrentMode != ViewMode.FocusedTable || _state.DrillDown is null)
    {
        return false;
    }

    _viewManager.ReturnFromDrillDown();
    return true;
}
```

- Added to the `baseKey switch` in `OnGlobalKeyDown` alongside `O`/`S`/`Q`/`T`/`X`/`C`.
- Added to `IsGlobalShortcut`'s key set, matching every other single-key global shortcut (this is
  what lets `MorphTreeView`/`MorphTableView.OnKeyDown` yield to the global handler for this key;
  `FocusedTableView` — a bare `MorphTableView` subclass with no Backspace handling of its own —
  already benefits from this without any change to it).
- Returns `false` (unhandled) outside `FocusedTable`, following the same conditional-return pattern
  `HandleViewToggle`/`HandleActionMenu` already use — no new precedent.

## 6. Discoverability: Status Bar Hint + Help Dialog

`Backspace` must be discoverable the same way every other single-key shortcut already is — via the
contextual status bar hints and the `?` help overlay. Both are updated:

**`ViewManager.RefreshStatusBarHints()`** — add one more contextual hint, alongside the existing
`t:Tree/Table`/`x:Menu`/`c:Clear` conditions:

```csharp
if (_state.CurrentMode == ViewMode.FocusedTable)
{
    hints.Add("bs:Back");
}
```

Shown only while in `FocusedTable` (i.e. only when the shortcut actually does something), matching
how `t:Tree/Table` and `x:Menu` are already gated to the modes where they apply. `bs` (not a Unicode
glyph) was chosen to match the short-letter style of every other hint (`o`, `s`, `t`, `x`, `c`) and
avoid terminal font rendering risk; the Help dialog spells it out in full (below) so the
abbreviation's meaning isn't left ambiguous anywhere in the UI.

**`HelpDialog.GetHelpText()`** — add one line under "Global / File Operations", next to the other
single-key bindings:

```
Backspace : Return from DrillDown to the originating tree view (FocusedTable only)
```

## Architectural Decision Records (ADR)

### ADR 1: `PreviousMode` Lives on `DrillDownState`, Not a Separate `AppState` Field

**Status:** Accepted

**Context:** The issue's own suggestion is "a 'previous mode' reference alongside
`AppState.DrillDown`." Two placements were possible: a standalone `AppState.PreviousMode` field set
whenever entering `FocusedTable`, or a field on `DrillDownState` itself.

**Decision:** Add `PreviousMode` to `DrillDownState`.

**Rationale:**
1. **Lifecycle correctness.** `DrillDownState` and "which mode to return to" are set at the exact
   same moment (DrillDown entry) and cleared at the exact same moment (`ReturnFromDrillDown`, or
   any future point that clears `_state.DrillDown`). A separate `AppState` field could be forgotten
   in one of those two places and silently drift out of sync with `DrillDown`; a field on the
   record itself cannot, by construction — there is no code path that sets/clears one without the
   other.
2. **Matches an existing project precedent.** `docs/design_breadcrumb_navigation.md` explicitly
   rejected adding new `AppState`/`DrillDownState` fields for "which DrillDown variant produced
   this" in favor of deriving it at the call site — the general bias here is toward attaching
   session-scoped data to the session's own record rather than scattering parallel `AppState`
   fields that must be manually kept in sync.

### ADR 2: Return to Tree Root, Do Not Restore Cursor/Scroll Position

**Status:** Accepted

**Context:** The issue notes recording "the originating `ViewMode` **or** tree cursor position" as
one way to close the gap. Restoring the exact cursor would mean, on `Backspace`: finding the tree
node matching a stored `KeyPath` inside a freshly rebuilt tree, expanding every ancestor on the
path to make it reachable, then setting `SelectedObject` and scrolling it into view.

**Decision:** Restore only `PreviousMode` (which tree, not where in it). Every `SwitchTo*Tree` call
rebuilds fresh at root, same as it does today for the `t`-key Table→Tree toggle.

**Rationale:**
1. **No existing capability to build on.** Nothing in the tree view layer today supports "select
   node by `KeyPath`" — trees are only ever built forward (root outward), never resolved backward
   (path to node). Building this is a nontrivial addition (path resolution + ancestor expansion +
   scroll-into-view) that doesn't exist for *any* other navigation flow in the app, DrillDown
   included on the way in.
2. **Root-return still fully solves the stated problem.** The issue is "there is no way to go
   back... the only way out is reopening the file." Landing on the correct tree, at its root, is a
   complete fix for that — the user is no longer stuck in `FocusedTable`, and re-navigating to the
   same node from a known-good tree is far cheaper than reopening the file from scratch.
3. **Consistent with how "going back" already behaves elsewhere.** The `t`-key toggle back to Tree
   mode already discards cursor state; a user returning via `Backspace` sees the same reset-to-root
   behavior they'd already get from `t`, rather than two different "return to tree" behaviors in
   the same app.

**Consequences:** A future issue could add KeyPath-based node resolution and restore the exact
cursor; this design does not block that. `DrillDownState.PreviousMode` alone is enough for this
issue's scope, so no `PreviousKeyPath` field is added now (`AppState.CurrentKeyPath` was
considered — it holds the KeyPath at DrillDown entry — but with no way to resolve a `KeyPath` back
to a tree node, storing it here would have no consumer).

## 7. Testing

- `ModeControllerTests.cs` — extend `DrillDown` tests to assert `DrillDownState.PreviousMode`
  equals `CurrentMode` as it was *before* the call; extend `FullAggregationDrillDownAsync` tests
  the same way.
- `ViewManagerTests.cs` — new tests for `ReturnFromDrillDown()`: one per `PreviousMode` case
  (`JsonLinesTree`/`JsonArrayTree` via `RowIndexer`, `JsonObjectTree` via `JsonObjectEntries`),
  asserting `CurrentMode` is restored and `DrillDown` is cleared; a no-op case when `DrillDown` is
  already `null`.
- `AppKeyHandlerTests.cs` — new tests for `Backspace`: handled + delegates to
  `ReturnFromDrillDown()` when `CurrentMode == FocusedTable`; unhandled (`false`) otherwise. Extend
  the existing `IsGlobalShortcut` theory data with `KeyCode.Backspace`.
- `AppStateTests.cs` — extend for `JsonObjectEntries` default (`null`).
- `FileDialogHandlerTests.cs` — extend: `JsonObjectEntries` is populated for `DataFormat.JsonObject`
  and reset to `null` for every other format.
- `TopLevelScannerTests.cs` / `TopLevelScannerTests.Scan.cs` — update existing assertions from
  tuple construction/comparison to `JsonObjectEntry` construction/comparison; no new test cases,
  same coverage against the new element type.
- `JsonObjectTreeViewTests.cs` — same signature-only update (tuple → `JsonObjectEntry`).

## 8. Files Touched

**Modified:**
- `src/App/Tui/AppState.cs` — add `JsonObjectEntries` (`IReadOnlyList<JsonObjectEntry>?`)
- `src/App/Tui/DrillDownState.cs` — add `PreviousMode`
- `src/App/Tui/Ui/ModeController.cs` — capture `PreviousMode` in `DrillDown` and
  `FullAggregationDrillDownAsync`
- `src/App/Tui/Ui/ViewManager.cs` — add `ReturnFromDrillDown()`, `Backspace` status bar hint,
  `SwitchToJsonObjectTree` parameter type → `IReadOnlyList<JsonObjectEntry>`
- `src/App/Tui/Ui/AppKeyHandler.cs` — add `HandleDrillDownBack()`, wire `Backspace` into
  `OnGlobalKeyDown` and `IsGlobalShortcut`
- `src/App/Tui/Ui/FileDialogHandler.cs` — reset/populate `JsonObjectEntries`
- `src/App/Tui/Ui/Views/Dialogs/HelpDialog.cs` — add `Backspace` line to help text
- `src/App/Tui/Ui/Views/JsonObjectTreeView.cs` — parameter type → `IReadOnlyList<JsonObjectEntry>`
- `src/Engine/IO/JsonObject/TopLevelScanner.cs` — element type swapped throughout, not just the
  `Scan` return type: `List<(string key, JsonRawBytes value)> result` → `List<JsonObjectEntry>`,
  threaded through `ProcessToken`/`RecordEntry`, and both tuple-literal construction sites
  (`result.Add((key, mem))`, `result[idx] = (key, mem)`) → `new JsonObjectEntry(key, mem)`
- `tests/Refedle.Tests/App/Tui/Ui/ModeControllerTests.cs`
- `tests/Refedle.Tests/App/Tui/Ui/ViewManagerTests.cs`
- `tests/Refedle.Tests/App/Tui/Ui/AppKeyHandlerTests.cs`
- `tests/Refedle.Tests/App/Tui/AppStateTests.cs`
- `tests/Refedle.Tests/App/Tui/Ui/FileDialogHandlerTests.cs`
- `tests/Refedle.Tests/App/Tui/Ui/Views/Dialogs/HelpDialogTests.cs`
- `tests/Refedle.Tests/App/Tui/Ui/Views/JsonObjectTreeViewTests.cs`
- `tests/Refedle.Tests/Engine/IO/JsonObject/TopLevelScannerTests.cs`
- `tests/Refedle.Tests/Engine/IO/JsonObject/TopLevelScannerTests.Scan.cs`

**New:**
- `src/Engine/IO/JsonObject/JsonObjectEntry.cs` — `readonly record struct JsonObjectEntry(string Key, JsonRawBytes Value)`
