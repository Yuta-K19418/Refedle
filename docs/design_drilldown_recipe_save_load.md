# Design: Recipe Save/Load for DrillDown Results

## In Scope

- Support saving a recipe while viewing a DrillDown result (`FocusedTableView`)
- Keep DrillDown-scoped actions separate from the base table's actions, so entering or leaving a DrillDown no longer discards either set
- Support loading a recipe that includes a DrillDown scope: automatically navigate to the recorded DrillDown location and replay the recorded actions there
- Add tests covering the above

## Out of Scope

- Re-DrillDown from a DrillDown result (multi-level DrillDown chaining)
- A recipe's format accommodates only a single DrillDown location (one KeyPath); combining multiple DrillDown locations into one recipe is out of scope. This applies regardless of DrillDown kind — both Single and Full Aggregation DrillDown remain supported per recipe, just one location at a time
- Recipe support for DrillDown scope in CLI headless mode (DrillDown is a TUI-only feature)

## Save Scope: Current View Only

Saving a recipe always captures the currently displayed table, and nothing else:

- `CurrentMode == FocusedTable` with an active `DrillDown` → the recipe captures `drillDownKeyPath`
  plus `DrillDownState.ActionStack`. The base table's `AppState.ActionStack` is not included, even
  if it has entries from before entering the DrillDown.
- Otherwise (`CurrentMode == Table`) → the recipe captures `AppState.ActionStack` only, unchanged
  from today's behavior — no `drillDownKeyPath` is written, even if `AppState.DrillDown` still holds
  a stale DrillDown (e.g. after navigating back out via Backspace without clearing it).

There is no combined save of both stacks in one recipe. To save the base table's actions after
having drilled down, navigate back to the tree view and switch to table mode (`t`, JSONLines only)
before saving — this matches the `CurrentMode == ViewMode.FocusedTable && DrillDown is not null`
guard already used throughout Phase 1 (`HandleMorphAction`, `AddContextualHints`, etc.).

## Implementation Phases

### Phase 1: Separate the DrillDown Action Stack from the base table's

`DrillDownState` gains its own `ActionStack`, mirroring how it already carries its own `Schema`
separately from `AppState.Schema`. Entering or leaving a DrillDown no longer clears either stack —
each is independent and untouched by the other.

`AppState.ActionStack`'s setter becomes `private`; all mutation goes through named methods
(`AddMorphAction`, `ClearMorphActions`, and a new `SetActionStack` for whole-list replacement,
needed by recipe load in a later phase). `DrillDownState` stays a plain data record — updates to
its `ActionStack` use a `with` expression directly at the call site, matching how `WorkingColumn`
is already updated elsewhere in the codebase (`LazyTransformerBase.ApplyRename` etc.), rather than
adding wrapper methods to the record.

```csharp
// DrillDownState.cs
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema,
    ViewMode PreviousMode,
    IReadOnlyList<MorphAction> ActionStack = []);
```

```csharp
// AppState.cs
private IReadOnlyList<MorphAction> _actionStack = [];
public IReadOnlyList<MorphAction> ActionStack => _actionStack;

internal void AddMorphAction(MorphAction action) => _actionStack = [.. _actionStack, action];
internal void ClearMorphActions() => _actionStack = [];
internal void SetActionStack(IReadOnlyList<MorphAction> actions) => _actionStack = actions;
```

Every call site that reads or writes the Action Stack decides explicitly, via a guard clause,
whether it targets the base table's stack or the active DrillDown's stack — no hidden branching
inside a shared property.

```csharp
// ViewManager.cs — HandleMorphAction
private void HandleMorphAction(MorphAction action)
{
    if (_state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is not null)
    {
        _state.DrillDown = _state.DrillDown with { ActionStack = [.. _state.DrillDown.ActionStack, action] };
        RefreshCurrentTableView();
        return;
    }

    _state.AddMorphAction(action);
    RefreshCurrentTableView();
}
```

```csharp
// ViewManager.cs — AddContextualHints
var currentActionCount = _state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is not null
    ? _state.DrillDown.ActionStack.Count
    : _state.ActionStack.Count;

if (currentActionCount > 0)
{
    hints.Add("c:Clear");
}
```

`DrillDown()`/`FullAggregationDrillDownAsync()` drop their `_state.ClearMorphActions()` call — a
freshly constructed `DrillDownState` already starts with `ActionStack: []`, and the base table's
stack must no longer be cleared on DrillDown entry. Construction order of `_state.DrillDown` and
`_state.CurrentMode` (verified in `ModeController.DrillDown`/`FullAggregationDrillDownAsync` and
`ViewManager.FullAggregationDrillDownAsync`) already sets `DrillDown` before `CurrentMode` flips to
`FocusedTable`, so no ordering change is needed elsewhere.

`AppKeyHandler.HandleQuit`/`HandleClearActions` apply the same explicit branching as
`AddContextualHints` for their Action Stack count checks; `HandleClearActions`' actual clear
branches the same way as `HandleMorphAction`.

`FileDialogHandler`'s new-file reset (`_state.ActionStack = [];`) becomes
`_state.ClearMorphActions();`. `RecipeCommandHandler.LoadFromPathAsync`'s
`_state.ActionStack = result.Value.Actions;` becomes `_state.SetActionStack(result.Value.Actions);`
— both required once the setter is private; the recipe DrillDown-scope handling itself is added in
a later phase.

**Unit tests:**
- `AppStateTests`: `AddMorphAction`/`ClearMorphActions`/`SetActionStack` mutate the base stack independently of any `DrillDownState`
- `ViewManagerTests`: `HandleMorphAction` (via a Morph action call) appends to `DrillDownState.ActionStack` when `CurrentMode` is `FocusedTable`, and to `AppState.ActionStack` otherwise; the other stack is left untouched in both cases
- `ViewManagerTests`: `DrillDown`/`FullAggregationDrillDownAsync` no longer clear the base table's `ActionStack`
- `AppKeyHandlerTests`: `HandleQuit`/`HandleClearActions` read/clear the correct stack depending on `CurrentMode`

| File | Change |
|---|---|
| `src/App/DrillDownState.cs` | Add `ActionStack` member (default `[]`) |
| `src/App/AppState.cs` | Private setter on `ActionStack`; add `SetActionStack` |
| `src/App/ViewManager.cs` | `HandleMorphAction`/`AddContextualHints` branch explicitly; drop `ClearMorphActions()` calls in `DrillDown()`/`FullAggregationDrillDownAsync()` |
| `src/App/AppKeyHandler.cs` | `HandleQuit`/`HandleClearActions` branch explicitly |
| `src/App/FileDialogHandler.cs` | Use `ClearMorphActions()` instead of direct assignment |
| `src/App/RecipeCommandHandler.cs` | Use `SetActionStack(...)` instead of direct assignment |

### Phase 2: `drillDownKeyPath` YAML format

A recipe saved from a DrillDown view records the location it was drilled into as a top-level
`drillDownKeyPath` sequence, alongside `actions`. Each `KeyPathSegment` (`src/Engine/IO/DrillDown/KeyPathSegment.cs`)
is either an object-property `Key` or an array-element `Index`; the YAML mirrors that distinction
with dedicated field names rather than exposing the `KeyPathSegmentKind` enum by name, so a reader
doesn't need to know the internal type to understand the file.

For a record shaped like:

```json
{
  "customer": {
    "name": "Acme Corp",
    "orders": [
      { "id": "ORD-001", "total": 42.50 },
      { "id": "ORD-002", "total": 15.00 }
    ]
  }
}
```

drilling into the first order (`customer.orders[0]`) serializes as:

```yaml
drillDownKeyPath:
  - key: "customer"
  - key: "orders"
    index: 0
```

Each sequence item is a mapping with `key` and/or `index`:
- `key` alone → one `Key` segment (e.g. `customer`, a plain object property)
- `index` alone → one `Index` segment (needed when an index isn't preceded by a key — e.g. a
  matrix-shaped field such as `"scores": [[10, 20], [30, 40]]`; drilling into `scores[1][0]`
  produces `- key: "scores"` followed by two bare `- index: 1` / `- index: 0` items, since the
  outer and inner array indices have no key between them)
- both present on the same item → a `Key` segment immediately followed by an `Index` segment (the
  common case shown above: indexing into an array-typed property, e.g. `orders[0]`); `key` always
  precedes `index` within an item, since a segment can only be indexed after being named
- neither present → invalid, rejected the same way `MorphActionParser.ParseAction` rejects an
  action dictionary missing `type`

This relies on plain YAML mapping-in-sequence syntax — a continuation line without a leading `-`,
indented to the same column as the field after `- `, is a second key in the same mapping as the
item above it, not a new sequence element. Verified against PyYAML; no custom parsing convention
is introduced.

`index` serializes as a plain integer (e.g. `index: 0`), and `RecipeYamlParser` converts it to the
`KeyPathSegment` label format (`Value: "[0]"`, per `KeyPathSegment`'s doc comment) when building the
segment.

**Parsing:** `RecipeYamlParser` gains a `DrillDownKeyPathItem` parse state alongside `ActionItem`.
The item-boundary check that currently triggers on the fixed prefix `"  - type: "` (`StartNewAction`,
`RecipeYamlParser.cs:82`) generalizes to triggering on any `"  - "` prefix for this section, since an
item's first field can be either `key` or `index` — unlike `actions`, where every item starts with
`type`.

**Unit tests:**
- `RecipeYamlSerializerTests`: a `Recipe` with `DrillDownKeyPath` serializes each segment as `- key: "..."`, `- index: N`, or both on one item (key immediately followed by an index); a `Recipe` with `DrillDownKeyPath: null` omits the section entirely
- `RecipeYamlParserTests`: parses a `key`-only item, an `index`-only item, and a combined `key`+`index` item into the right `KeyPathSegment` sequence and order; rejects an item with neither field (mirroring the existing missing-`type` rejection for actions); rejects a malformed `index` value (non-integer); round-trips serialize→parse for a multi-segment path including a leading bare `index` (array-of-arrays case)

### Phase 3: Save flow

`Recipe` gains an optional `DrillDownKeyPath` property. `Recipe` stays `sealed` — the only
difference between a base-table recipe and a DrillDown-scoped one is this single field, which does
not justify a type hierarchy (a discriminated union like `MorphAction`'s subtypes is worth
revisiting only if a later property varies in kind, not just in presence).

```csharp
// Recipe.cs
public IReadOnlyList<KeyPathSegment>? DrillDownKeyPath { get; init; }
```

`DrillDownState` gains a `KeyPath` alongside the `ActionStack` added in Phase 1. Both DrillDown
entry points already receive a `KeyPath` on their request (`SingleDrillDownRequest.KeyPath`,
`FullAggregationDrillDownRequest.KeyPath` — `DrillDownRequest.cs:14,23`); `ModeController` currently
discards it when constructing `DrillDownState` (`ModeController.cs:116,146`) and just needs to pass
it through — no new capture logic required.

```csharp
// DrillDownState.cs
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema,
    ViewMode PreviousMode,
    IReadOnlyList<KeyPathSegment> KeyPath,
    IReadOnlyList<MorphAction> ActionStack = []);
```

`RecipeCommandHandler.SaveAsync`'s mode guard extends to allow `ViewMode.FocusedTable`
(`RecipeCommandHandler.cs:23`, currently `CsvTable or JsonLinesTable or JsonLinesTree`). Recipe
construction branches per the [Save Scope](#save-scope-current-view-only) rule: `CurrentMode ==
FocusedTable && DrillDown is not null` builds from `_state.DrillDown.ActionStack` and
`_state.DrillDown.KeyPath`; otherwise it builds from `_state.ActionStack` as today, leaving
`DrillDownKeyPath` unset.

```csharp
// RecipeCommandHandler.cs — SaveAsync
var recipe = _state.CurrentMode == ViewMode.FocusedTable && _state.DrillDown is not null
    ? new Recipe
      {
          Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
          Actions = _state.DrillDown.ActionStack,
          DrillDownKeyPath = _state.DrillDown.KeyPath,
          LastModified = System.DateTimeOffset.UtcNow,
      }
    : new Recipe
      {
          Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
          Actions = _state.ActionStack,
          LastModified = System.DateTimeOffset.UtcNow,
      };
```

**Unit tests:**
- `RecipeCommandHandlerTests`: `SaveAsync` from `FocusedTable` with an active `DrillDown` builds a `Recipe` with `DrillDownKeyPath` set to `DrillDown.KeyPath` and `Actions` set to `DrillDown.ActionStack` (not `AppState.ActionStack`); `SaveAsync` from `Table` mode builds a `Recipe` with `DrillDownKeyPath` unset and `Actions` from `AppState.ActionStack`, even when a stale `AppState.DrillDown` is present
- `ModeControllerTests`: `DrillDown`/`FullAggregationDrillDownAsync` populate `DrillDownState.KeyPath` from the request's `KeyPath`

### Phase 4: Load flow

Replaying a recorded `drillDownKeyPath` differs by DrillDown kind:

**Full Aggregation DrillDown** (JSON Lines / JSON Array) replays for free: `FullAggregationScanner.Scan`
already re-scans the whole file driven purely by a `KeyPath` (`ModeController.cs:137-139`), so load just
rebuilds a `FullAggregationDrillDownRequest` from `recipe.DrillDownKeyPath` and calls
`FullAggregationDrillDownAsync` — no new code.

**Single DrillDown** (JSON Object) needs a new resolution step. `SingleDrillDownRequest` requires
`NodeBytes` — the raw bytes of the target array — which today only ever comes from an interactively
selected `JsonArrayTreeNode.RawJson` (`AppKeyHandler.cs:239`). On load there is no tree selection, only
the recorded `KeyPath`, so `NodeBytes` must be resolved by walking the file's root bytes down that path.

There is no single contiguous root `JsonRawBytes` to start from: `AppState.JsonObjectEntries`
(`AppState.cs:74`) holds only the top-level key/value pairs `TopLevelScanner.Scan` extracted, not
the whole file's bytes. Resolving `keyPath[0]` (always a `Key` — the JSON Object root can't be an
array) against `JsonObjectEntries` is an App-layer concern (it needs `AppState`), so it happens in
`RecipeCommandHandler`, not inside `KeyPathTraverser`. `ResolveSingleNode` itself starts from
whatever bytes that first lookup produced and walks the rest of the path:

```csharp
// KeyPathTraverser.cs
public static Result<JsonRawBytes> ResolveSingleNode(JsonRawBytes startBytes, IReadOnlyList<KeyPathSegment> remainingKeyPath)
```

Unlike `ExtractRows`, this has no branching to explore — each segment has exactly one destination
node, so it's a plain loop (no stack, no `TraversalFrame`), not a variant of the existing DFS:
- `Key` segment → `KeyPathLeafCollector.FindValueByKey` (existing)
- `Index` segment → a new `KeyPathLeafCollector` helper that parses the segment's `[n]` label back to
  an integer and returns that specific array element's bytes, keeping the one-way dependency
  (`KeyPathTraverser` → `KeyPathLeafCollector`) the class docs already establish
  (`KeyPathTraverser.cs:12-13`)

It returns `Result<JsonRawBytes>`, not the silent-skip semantics `ExtractRows` uses for missing
keys/type mismatches (`KeyPathTraverser.cs:127`, `145`) — a recipe load must surface an explicit error
when the recorded path no longer resolves (e.g. the underlying file changed), rather than silently
producing an empty DrillDown.

**Orchestration.** `RecipeCommandHandler.LoadFromPathAsync` branches on `recipe.DrillDownKeyPath`.
The DrillDown branch is extracted into its own method (`LoadDrillDownRecipeAsync`) rather than
inlined — combined into one method, the format branch nested inside the DrillDownKeyPath branch
would likely trip the analyzer's cyclomatic-complexity threshold under the project's zero-warnings
policy, on top of exceeding the 2-level nesting limit:

```
LoadFromPathAsync(path):
  recipe = await _recipeManager.LoadAsync(path)
  if recipe.DrillDownKeyPath is null:
      _state.SetActionStack(recipe.Actions)
      _viewManager.RefreshCurrentTableView()
      return

  await LoadDrillDownRecipeAsync(recipe)
  _viewManager.RefreshCurrentTableView()

LoadDrillDownRecipeAsync(recipe):
  format = FormatDetector.Detect(_state.CurrentFilePath)
  if format != JsonObject:                              // Full Aggregation DrillDown
      request = FullAggregationDrillDownRequest(format, recipe.DrillDownKeyPath)
      await _viewManager.FullAggregationDrillDownAsync(request)
      _state.DrillDown = _state.DrillDown with { ActionStack = recipe.Actions }
      return

  // Single DrillDown
  entry = _state.JsonObjectEntries.FirstOrDefault(e => e.Key == recipe.DrillDownKeyPath[0].Value)
  if entry not found: fail — recorded path no longer matches this file
  nodeBytes = KeyPathTraverser.ResolveSingleNode(entry.Value, recipe.DrillDownKeyPath[1..])
  if nodeBytes is failure: fail — propagate the resolution error
  request = SingleDrillDownRequest(format, nodeBytes.Value, recipe.DrillDownKeyPath)
  _viewManager.DrillDown(request)
  _state.DrillDown = _state.DrillDown with { ActionStack = recipe.Actions }
```

`RecipeCommandHandler` already holds the `ViewManager` reference `AppKeyHandler` uses for the same
two calls (`AppKeyHandler.cs:242`, `260`), so no new dependency is needed.

**Unit tests:**
- `KeyPathTraverserTests`: `ResolveSingleNode` with an empty `remainingKeyPath` returns `startBytes` unchanged; with `Key`-only segments descends via nested object lookups; with an `Index` segment selects that specific array element (not all elements, unlike `ExtractRows`); returns failure for a missing key, a type mismatch (e.g. `Index` segment against a non-array), and an out-of-range index
- `RecipeCommandHandlerTests`: `LoadFromPathAsync` with `DrillDownKeyPath: null` sets `AppState.ActionStack` only (existing behavior, unchanged); with a JSON Object recipe whose first segment matches `JsonObjectEntries`, navigates via `SingleDrillDownRequest` and sets `DrillDown.ActionStack` from `recipe.Actions`; with a JSON Lines/Array recipe, navigates via `FullAggregationDrillDownRequest`; with a first segment absent from `JsonObjectEntries` (file changed since save), surfaces a failure instead of silently loading the base table

### Phase 5: E2E

Following the existing `MainWindowTests.SaveRecipeAction.cs` style (real key-press simulation
through `Harness`, save/load against a temp `.yaml` file):

- Single DrillDown (JSON Object): drill into an array field, perform an action, save — assert the
  written YAML contains both `drillDownKeyPath` and `actions`; then load that same recipe into a
  fresh session and assert the app lands back on the DrillDown table with the action already applied
- Full Aggregation DrillDown (JSON Array): same round trip, via the Full Aggregation entry point

