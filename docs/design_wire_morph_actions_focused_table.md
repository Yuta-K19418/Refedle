# Design: Wire Morph Actions for FocusedTableView

## In Scope

- Enable Morph actions (Rename/Delete/Cast/Filter/Fill/FormatTimestamp) on `FocusedTableView`, the table shown after DrillDown
- Apply to all three DrillDown entry points (JSON Object, JSON Lines, JSON Array)
- Prevent actions applied to the base table from unintentionally carrying over into DrillDown results
- Add unit tests covering the above

## Out of Scope

- Memory optimization for FocusedTable (follow-up work)
- Behavior changes to the existing Csv/JsonLines table views
- Re-DrillDown from a DrillDown result
- Recipe save/load for actions applied on a DrillDown result
- Background/async execution of `FocusedTableTransformer`'s Filter evaluation — unlike Csv/JsonLines' `IFilterRowIndexer` (built via `Task.Run`, off the UI thread), `FocusedTableTransformer.Create` resolves matching rows synchronously on the UI thread. A DrillDown result with a very large row count could cause a brief UI freeze when a Filter action is applied

## Implementation Phases

### Phase 1: Extract `LazyTransformerBase`

Split the schema-transformation logic out of `LazyTransformer` into a new
abstract base class, `LazyTransformerBase`, following the project's existing
`XxxBase` convention (`RangeTreeViewBase`, `RowIndexerBase`). Row/column
resolution against the underlying `ITableSource` stays out of the base class
and remains the responsibility of each concrete transformer, since it differs
between the two (`IFilterRowIndexer`-backed for `LazyTransformer` vs. an
in-memory synchronous resolution for the new FocusedTable transformer added
in Phase 2).

`LazyTransformerBase` implements `ITableSource` directly, matching the
project's existing `XxxBase : IXxx` convention (`RowIndexerBase : IRowIndexer`).
Since the base owns the interface, it also owns `Source` — the wrapped
`ITableSource` both derived transformers decorate — along with its disposal.

**Moves to `LazyTransformerBase` (private → protected, except where noted):**
- `BuildTransformedSchema` and the `ApplyAction`/`ApplyRename`/`ApplyCast`/
  `ApplyFilter`/`ApplyFill`/`ApplyFormatTimestamp` helpers
- `FormatCellValue` and its `FormatWholeNumber`/`FormatFloatingPoint`/
  `FormatBoolean`/`FormatTimestamp` helpers
- The `WorkingColumn` record
- `public string[] ColumnNames { get; }` / `internal string[] RawColumnNames { get; }`
  (still public/internal — required by `ITableSource` and by
  `ViewManager`'s `GetRawColumnName` callback)
- `public int Columns => ColumnNames.Length;`
- `protected IReadOnlyList<ColumnType> ColumnTypes { get; }`
- `protected IReadOnlyList<int> SourceColumnIndices { get; }`
- `protected IReadOnlyList<string?> FillValues { get; }`
- `protected IReadOnlyList<string?> FormatStrings { get; }`
- `protected ITableSource Source { get; }` — the wrapped source, common to
  every transformer
- `IDisposable` itself: `public void Dispose()` + `protected virtual void
  Dispose(bool disposing)`, disposing `Source` when it is `IDisposable`.
  Required because the base is inheritable — an analyzer (CA1063/CA1816)
  would otherwise flag a non-virtual `Dispose` on a base class

**Stays on `LazyTransformer` (the existing Csv/JsonLines transformer):**
- `_filterRowIndexer`
- `Rows`, `this[row, col]`

**Factory method:** `LazyTransformer`'s constructor becomes `private`; a
`public static Create(...)` factory takes over instantiation. The base
constructor stays a plain field assignment — the heavy work
(`BuildTransformedSchema`, filter-indexer construction) moves to `Create`.

```csharp
internal abstract class LazyTransformerBase : ITableSource, IDisposable
{
    private bool _disposed;

    protected LazyTransformerBase(
        ITableSource source,
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings)
    {
        Source = source;
        ColumnNames = columnNames;
        RawColumnNames = rawColumnNames;
        ColumnTypes = columnTypes;
        SourceColumnIndices = sourceColumnIndices;
        FillValues = fillValues;
        FormatStrings = formatStrings;
    }

    protected ITableSource Source { get; }
    public string[] ColumnNames { get; }
    internal string[] RawColumnNames { get; }
    public int Columns => ColumnNames.Length;
    protected IReadOnlyList<ColumnType> ColumnTypes { get; }
    protected IReadOnlyList<int> SourceColumnIndices { get; }
    protected IReadOnlyList<string?> FillValues { get; }
    protected IReadOnlyList<string?> FormatStrings { get; }

    public abstract int Rows { get; }
    public abstract object this[int row, int col] { get; }

    protected static (
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings,
        IReadOnlyList<FilterSpec> filterSpecs
    ) BuildTransformedSchema(TableSchema originalSchema, IReadOnlyList<MorphAction> actions) { /* moved as-is */ }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Source is IDisposable d)
        {
            d.Dispose();
        }
    }
}
```

```csharp
internal sealed class LazyTransformer : LazyTransformerBase
{
    private readonly IFilterRowIndexer? _filterRowIndexer;

    private LazyTransformer(
        ITableSource source,
        IFilterRowIndexer? filterRowIndexer,
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings)
        : base(source, columnNames, rawColumnNames, columnTypes, sourceColumnIndices, fillValues, formatStrings)
    {
        _filterRowIndexer = filterRowIndexer;
    }

    public static LazyTransformer Create(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions,
        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer>? filterRowIndexerFactory = null)
    {
        var schema = BuildTransformedSchema(originalSchema, actions);
        var filterRowIndexer = schema.filterSpecs.Count > 0 && filterRowIndexerFactory is not null
            ? filterRowIndexerFactory(schema.filterSpecs)
            : null;

        return new LazyTransformer(
            source, filterRowIndexer,
            schema.columnNames, schema.rawColumnNames, schema.columnTypes,
            schema.sourceColumnIndices, schema.fillValues, schema.formatStrings);
    }

    internal IFilterRowIndexer? FilterRowIndexer => _filterRowIndexer;
    public override int Rows => _filterRowIndexer?.TotalMatchedRows ?? Source.Rows;
    public override object this[int row, int col] { get { /* unchanged, reads via Source */ } }
}
```

Call sites in `ViewManager.SwitchToCsvTable`/`SwitchToJsonLinesTableView` change
from `new Views.LazyTransformer(...)` to `Views.LazyTransformer.Create(...)`.
The new FocusedTable transformer added in Phase 2 follows the same factory
pattern for consistency.

This phase is a pure refactor — `LazyTransformer`'s external behavior must be
unchanged, verified by the existing Csv/JsonLines test suite.

**Unit tests:** no new test cases — the existing `LazyTransformerTests` suite
must keep passing unmodified, proving the refactor didn't change behavior.

| File | Change |
|---|---|
| `src/App/Views/LazyTransformerBase.cs` (new) | Abstract base class holding shared schema-transformation logic |
| `src/App/Views/LazyTransformer.cs` | Refactored to inherit `LazyTransformerBase`; keeps only filter-row-indexer-backed row/column resolution |

### Phase 2: Add `FocusedTableTransformer`

A new `LazyTransformerBase` subclass for `FocusedTableSource`. It has no
`IFilterRowIndexer` — `FocusedTableSource` already holds every row in memory,
so filtering is resolved synchronously in `Create`, reusing the existing
stateless `FilterEvaluator.EvaluateFilter` (the same evaluator
`FilterRowIndexer` already uses per-cell) instead of introducing a new
filter engine.

`FocusedTableSource`'s column 0 is always `"#"` (a pre-computed per-row hash,
not part of the schema); real data columns start at index 1. Per the
earlier decision, `"#"` is never a Morph target: `Create` runs
`BuildTransformedSchema` over the DrillDown schema only (which has no `"#"`
entry), then prepends a fixed `"#"` slot to every output array before calling
the base constructor. Because `"#"` is absent from `BuildTransformedSchema`'s
internal `nameToIndex` map, any action that happens to target it is silently
skipped by the existing logic — no special-casing needed there. The `+1`
column offset (schema column index → actual `FocusedTableSource` column
index) is applied in `this[row, col]`.

```csharp
internal sealed class FocusedTableTransformer : LazyTransformerBase
{
    private readonly IReadOnlyList<int>? _matchedRowIndices;

    private FocusedTableTransformer(
        ITableSource source,
        IReadOnlyList<int>? matchedRowIndices,
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings)
        : base(source, columnNames, rawColumnNames, columnTypes, sourceColumnIndices, fillValues, formatStrings)
    {
        _matchedRowIndices = matchedRowIndices;
    }

    public static FocusedTableTransformer Create(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions)
    {
        var schema = BuildTransformedSchema(originalSchema, actions);

        string[] columnNames = ["#", .. schema.columnNames];
        string[] rawColumnNames = ["#", .. schema.rawColumnNames];
        IReadOnlyList<ColumnType> columnTypes = [ColumnType.Text, .. schema.columnTypes];
        IReadOnlyList<int> sourceColumnIndices = [-1, .. schema.sourceColumnIndices];
        IReadOnlyList<string?> fillValues = [null, .. schema.fillValues];
        IReadOnlyList<string?> formatStrings = [null, .. schema.formatStrings];

        var matchedRowIndices = schema.filterSpecs.Count > 0
            ? ResolveMatchedRows(source, schema.filterSpecs)
            : null;

        return new FocusedTableTransformer(
            source, matchedRowIndices,
            columnNames, rawColumnNames, columnTypes,
            sourceColumnIndices, fillValues, formatStrings);
    }

    // AND semantics across all specs, matching FilterRowIndexer (Csv/JsonLines).
    private static IReadOnlyList<int> ResolveMatchedRows(
        ITableSource source, IReadOnlyList<FilterSpec> filterSpecs)
    {
        List<int> matched = [];
        for (var row = 0; row < source.Rows; row++)
        {
            var isMatch = true;
            foreach (var spec in filterSpecs)
            {
                var rawValue = Convert.ToString(
                    source[row, spec.SourceColumnIndex + 1], CultureInfo.InvariantCulture) ?? string.Empty;
                isMatch = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);
                if (!isMatch)
                {
                    break;
                }
            }

            if (isMatch)
            {
                matched.Add(row);
            }
        }

        return matched;
    }

    public override int Rows => _matchedRowIndices?.Count ?? Source.Rows;

    public override object this[int row, int col]
    {
        get
        {
            var sourceRow = _matchedRowIndices?[row] ?? row;

            if (col == 0)
            {
                return Source[sourceRow, 0]; // "#" passthrough, never transformed
            }

            var fillValue = FillValues[col];
            if (fillValue is not null)
            {
                return fillValue;
            }

            var sourceCol = SourceColumnIndices[col] + 1; // +1 skips the "#" pseudo column
            var rawValue = Convert.ToString(Source[sourceRow, sourceCol], CultureInfo.InvariantCulture) ?? string.Empty;
            return FormatCellValue(rawValue, ColumnTypes[col], FormatStrings[col]);
        }
    }
}
```

**`FocusedTableSource` column-name labeling:** `VirtualTableSource` (Csv) and
`JsonLinesTableSource` both build `ColumnNames` as `"{Name} ({Type label})"`
and expose a separate `internal string[] RawColumnNames` for the unlabeled
names. `FocusedTableSource` currently uses the raw name for both display and action
targeting, with no type label. Bring it in line with the same convention so
DrillDown tables show `(Text)`/`(Number)`/etc. headers, and so `ViewManager`
can resolve raw column names via `RawColumnNames` (matching how it already
does this for `VirtualTableSource`/`JsonLinesTableSource`) instead of
reaching into `drillDown.Schema` directly.

```csharp
internal sealed class FocusedTableSource : ITableSource
{
    private readonly IReadOnlyList<FocusedTableRow> _rows;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;
    private readonly string[] _rawColumnNames;
    private readonly byte[][] _columnNamesUtf8;

    internal FocusedTableSource(DrillDownState drillDown)
    {
        ArgumentNullException.ThrowIfNull(drillDown);
        _rows = drillDown.Rows;
        _schema = drillDown.Schema;
        _columnNames = ["#", .. drillDown.Schema.Columns.Select(c => $"{c.Name} ({ColumnTypeLabel.ToLabel(c.Type)})")];
        _rawColumnNames = ["#", .. drillDown.Schema.Columns.Select(c => c.Name)];
        _columnNamesUtf8 = [.. drillDown.Schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))];
    }

    // Rows / Columns / this[row, col] unchanged.

    public string[] ColumnNames => _columnNames;
    internal string[] RawColumnNames => _rawColumnNames;
}
```

**Unit tests (`FocusedTableTransformerTests`):**
- Each of the 6 actions (Rename/Delete/Cast/Filter/Fill/FormatTimestamp) transforms `ColumnNames`/`RawColumnNames`/cell values as expected
- `"#"` stays at output column 0 unaffected by any action, including one that targets the literal name `"#"` (silently skipped)
- Filter narrows `Rows` and `this[row, col]` to only matching source rows, with multiple `FilterAction`s combined by AND (matching `FilterRowIndexer`)
- Column-index resolution accounts for the `"#"` offset (`SourceColumnIndices[col] + 1`) against the wrapped `FocusedTableSource`

| File | Change |
|---|---|
| `src/App/Views/FocusedTableTransformer.cs` (new) | `LazyTransformerBase` subclass for DrillDown results; synchronous filter, `"#"` passthrough |
| `src/App/Views/FocusedTableSource.cs` | Adds type-labeled `ColumnNames` and `internal RawColumnNames`, matching `VirtualTableSource`/`JsonLinesTableSource` |

**Update existing E2E assertions for type labels:** the `ColumnNames`
labeling change means `FocusedTableView` headers now render as
`"name (text)"`/`"val (number)"` instead of the bare column name. The
existing loose `Contains("name")`-style assertions in
`MainWindowTests.DrillDownSingle.cs` and
`MainWindowTests.DrillDownFullAggregation.cs` still pass as-is (they're
substring checks), but they should be tightened to assert the labeled form
explicitly, so the tests actually verify the new labeling behavior rather
than merely tolerating it:

- `DrillDownSingle.cs` (`DrillDown_OnJsonObjectTree_RendersSingleModeFocusedTable`):
  `WaitForContentsAsync("name", "val")` → `WaitForContentsAsync("name (text)", "val (number)")`
- `DrillDownFullAggregation.cs` (`DrillDown_OnJsonArrayTree_RendersUnionSchemaAcrossAllElements`):
  `WaitForContentsAsync("name", "age", "email")` → `WaitForContentsAsync("name (text)", "age (number)", "email (text)")`,
  and the header-line assertion's `Contains` calls updated to match

| File | Change |
|---|---|
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.DrillDownSingle.cs` | Tighten header assertion to the labeled column-name form |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.DrillDownFullAggregation.cs` | Tighten header assertion to the labeled column-name form |

### Phase 3: Wire `ViewManager`

**`SwitchToFocusedTable`:** wrap with `FocusedTableTransformer` only when
`ActionStack` is non-empty, matching the existing `SwitchToCsvTable`/
`SwitchToJsonLinesTableView` convention (avoids an unnecessary wrapper layer
when there are no actions to apply). Set `OnMorphAction`/`GetRawColumnName`
on the view either way, so Morph actions work from the first DrillDown even
before any action has been applied.

```csharp
internal void SwitchToFocusedTable(DrillDownState drillDown)
{
    ObjectDisposedException.ThrowIf(_disposed, this);

    ITableSource rawSource = new Views.FocusedTableSource(drillDown);
    var source = _state.ActionStack.Count > 0
        ? Views.FocusedTableTransformer.Create(rawSource, drillDown.Schema, _state.ActionStack)
        : rawSource;

    Func<int, string> getRawColumnName = source switch
    {
        Views.FocusedTableTransformer ft => i => ft.RawColumnNames[i],
        Views.FocusedTableSource fts => i => fts.RawColumnNames[i],
        _ => throw new UnreachableException(),
    };

    var view = new Views.FocusedTableView
    {
        Table = source,
        Style = new TableStyle { AlwaysShowHeaders = true },
        OnMorphAction = HandleMorphAction,
        GetRawColumnName = getRawColumnName,
    };
    _state.OnSchemaRefined = null;
    view.SetSelection(0, 0, false);
    view.Update();
    SwapView(view);
    view.SetFocus();
    RefreshStatusBarHints();
}
```

**`RefreshCurrentTableView`:** add a `ViewMode.FocusedTable` case so a Morph
action applied from `FocusedTableView` actually re-renders (today this
`switch` has no case for it, so `HandleMorphAction` would silently no-op for
DrillDown results).

```csharp
internal void RefreshCurrentTableView()
{
    switch (_state.CurrentMode)
    {
        case ViewMode.CsvTable when _state.RowIndexer is not null && _state.Schema is not null:
            SwitchToCsvTable(_state.RowIndexer, _state.Schema);
            break;

        case ViewMode.JsonLinesTable
            when _state.RowIndexer is not null && _state.Schema is not null:
            SwitchToJsonLinesTableView(_state.RowIndexer, _state.Schema);
            break;

        case ViewMode.FocusedTable when _state.DrillDown is not null:
            SwitchToFocusedTable(_state.DrillDown);
            break;

        default:
            break;
    }
}
```

**Clear `ActionStack` on DrillDown entry:** call `_state.ClearMorphActions()`
in both `DrillDown` and `FullAggregationDrillDownAsync`, only after the
DrillDown itself succeeds — a failed DrillDown leaves the previous table view
(and its actions) untouched.

```csharp
internal void DrillDown(SingleDrillDownRequest request)
{
    var result = _modeController.DrillDown(request);

    _uiThreadInvoke(() =>
    {
        if (result.IsFailure)
        {
            ShowError(result.Error);
            return;
        }

        if (_state.DrillDown is not { } drillDown)
        {
            throw new UnreachableException(
                "ModeController.DrillDown must set DrillDown state on success.");
        }

        _state.ClearMorphActions();
        UpdateBreadcrumb(request.KeyPath, collapseIndices: false);
        SwitchToFocusedTable(drillDown);
    });
}
```

```csharp
internal async ValueTask FullAggregationDrillDownAsync(FullAggregationDrillDownRequest request)
{
    var result = await _modeController.FullAggregationDrillDownAsync(request);
    _uiThreadInvoke(() =>
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (result.IsFailure)
        {
            ShowError(result.Error);
            return;
        }

        _state.DrillDown = result.Value;
        _state.CurrentMode = ViewMode.FocusedTable;
        _state.ClearMorphActions();
        UpdateBreadcrumb(request.KeyPath, collapseIndices: true);
        SwitchToFocusedTable(result.Value);
    });
}
```

**Unit tests (`ViewManagerTests`):**
- `SwitchToFocusedTable` sets `OnMorphAction`/`GetRawColumnName` on the resulting `FocusedTableView`
- `RefreshCurrentTableView` re-renders via `SwitchToFocusedTable` when `CurrentMode` is `FocusedTable` and `DrillDown` is set
- `DrillDown`/`FullAggregationDrillDownAsync` clear `ActionStack` on success, and leave it untouched on failure

| File | Change |
|---|---|
| `src/App/ViewManager.cs` | `SwitchToFocusedTable` wires `OnMorphAction`/`GetRawColumnName`; `RefreshCurrentTableView` gains a `FocusedTable` case; `DrillDown`/`FullAggregationDrillDownAsync` clear `ActionStack` on success |

### Phase 4: E2E tests

New TUI E2E test methods, added to the existing per-action partial-class
files in `tests/Refedle.E2ETests/Tui/MainWindow/` — matching the pattern
`MainWindowTests.SaveRecipeAction.cs` already uses, where one file holds
both a Csv-table test and a JsonLines-tree test as separate `[Fact]`s. Each
of the 6 existing action files gains one additional `OnFocusedTable`-suffixed
test method (JSON Array) alongside its existing Csv test; no new files for
the actions themselves. DrillDown itself, across all three entry points,
already has coverage in `MainWindowTests.DrillDownSingle.cs` and
`MainWindowTests.DrillDownFullAggregation.cs`.

- `MainWindowTests.RenameColumnAction.cs` — add `ActionMenu_RenameColumnOnFocusedTable_RendersNewHeaderWithUnchangedValues`
- `MainWindowTests.DeleteColumnAction.cs` — add `ActionMenu_DeleteColumnOnFocusedTable_RemovesColumnFromRenderedTable`
- `MainWindowTests.CastColumnAction.cs` — add `ActionMenu_CastColumnOnFocusedTable_RendersNewColumnTypeSuffix`
- `MainWindowTests.FilterColumnAction.cs` — add `ActionMenu_FilterColumnOnFocusedTable_RendersOnlyMatchingRows`
- `MainWindowTests.FillColumnAction.cs` — add `ActionMenu_FillColumnOnFocusedTable_RendersFillValueInEveryCellOfColumn`
- `MainWindowTests.FormatTimestampAction.cs` — add `ActionMenu_FormatTimestampColumnOnFocusedTable_RendersReformattedTimestamps`

**`c` (ClearActions) on `FocusedTableView`:** `AppKeyHandler.HandleClearActions`
calls `_state.ClearMorphActions()` then `_viewManager.RefreshCurrentTableView()`
— the exact `RefreshCurrentTableView` path Phase 3 adds a `FocusedTable` case
to. Unlike the DrillDown-entry clearing above, this exercises real rendering
(the confirmation dialog and the re-render), so it belongs at the E2E level.
Add to the existing `MainWindowTests.ClearActions.cs` as a second `[Fact]`:
- `ClearActionsKey_OnFocusedTableWithPendingAction_RevertsToOriginalRendering`
  — apply a Rename on a `FocusedTableView` (JSON Array DrillDown), press `c`,
  confirm, and assert the header reverts to the pre-rename name

| File | Change |
|---|---|
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.RenameColumnAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.DeleteColumnAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.CastColumnAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.FilterColumnAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.FillColumnAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.FormatTimestampAction.cs` | Add `OnFocusedTable` test (JSON Array) |
| `tests/Refedle.E2ETests/Tui/MainWindow/MainWindowTests.ClearActions.cs` | Add `OnFocusedTable` test (JSON Array) |
