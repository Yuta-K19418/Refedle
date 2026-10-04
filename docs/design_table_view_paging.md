# Design: Table View Paging for More Than int.MaxValue Rows

## Context

- Terminal.Gui 2.5.0's `ITableSource` is int-based (`int Rows`, `this[int row, int col]`),
  so `TableView` cannot address tables with more than `int.MaxValue` rows.
- The rows the application handles internally are addressed as `long`; only the exchange
  with `TableView` must stay `int`.
- In every option below, only the outermost layer implements `ITableSource`. The inner layers
  (`ColumnWidthStabilizingTableSource`, `LazyTransformer` / `FocusedTableTransformer`,
  `VirtualTableSource` / `JsonLinesTableSource` / `FocusedTableSource`) address rows as `long`
  and know nothing about pages.
- In every option, page or window positions are internal state only; the user sees one
  continuous table.

## Options

### Option A: Rewrite the page number

- The outermost layer holds a mutable page number. `TableView.Table` is never replaced.
- It exposes the current page plus its neighbors (three pages; two at the first and last page),
  so the page size is at most `int.MaxValue / 3`.
- Absolute row = (first visible page − 1) × page size + displayed row,
  where first visible page = max(1, page number − 1).
- When the cursor crosses a page boundary, `MorphTableView` updates the page number and
  shifts the selected row and the scroll offset by however much the window start moved
  (zero when moving from page 1 to page 2; any number of pages for `gg` / `G`).
- While correcting the selection, a flag (`_isAdjustingSelection`) suspends boundary detection,
  because the correcting `SetSelection` fires the selection-change event again.

Pros:

- `Table` is never replaced, so the cursor is never reset to the top. No logic to save and
  restore the cursor is needed.
- Page boundaries are fixed, so the arithmetic and its tests stay simple.

Cons:

- The outermost layer's contents change in place, so `TableView` must be told that `Rows`
  changed.
- Moving the cursor back and forth right at a page boundary switches pages each time
  (the switch is cheap, so the practical impact is small).

### Option B: Rebuild and replace an immutable range layer

- The outermost layer has a fixed range set at construction and cannot change.
- When the cursor crosses a range boundary, `MorphTableView` builds a new range layer for the
  shifted range and assigns it to `TableView.Table`. Only the range layer is rebuilt; the cache
  and filter layers are reused.

Pros:

- The range layer is immutable, so its state never changes mid-flight and is easy to reason about.
- The `Table` setter recalculates everything, so there is no need to report a `Rows` change.

Cons:

- The `Table` setter resets the cursor to (0, 0) and fires the selection-change event.
  Saving and restoring the cursor and scroll position is required on every swap; this adds
  code and room for bugs. As in Option A, a flag that suspends boundary detection is also
  needed.
- Whether the momentary jump to the top is ever visible was not verified on a real terminal.
- Ownership of disposing the inner layers must move to `MorphTableView`.

### Option C: Shift a window start row

- The outermost layer holds a mutable window start row. `TableView.Table` is never replaced.
- When the cursor gets within a set distance of a window edge, the window start is moved by an
  arbitrary amount (for example, so the cursor lands in the middle of the window).
- Absolute row = window start + displayed row.

Pros:

- All of Option A's pros.
- After a shift the cursor sits mid-window, so moving back and forth near an edge does not
  shift the window repeatedly.

Cons:

- Shares Option A's first con.
- More parameters to decide (window size, edge distance, shift amount); the first and last
  windows are slightly more complex, and more test cases are needed.

## Decision

Adopt **Option A**.

- Option B is rejected: every swap needs logic to save and restore the cursor and scroll
  position, which makes it the most complex option.
- Option C is rejected: its only advantage over Option A (no repeated shifting near a
  window edge) has little value, because Option A rarely reaches a page boundary and the
  switch itself is cheap. In exchange, it adds parameters to decide (window size, edge
  distance, shift amount), more complex handling at the first and last windows, and more
  test cases.
