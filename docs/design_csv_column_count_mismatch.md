# Design: Handling CSV Rows with a Mismatched Column Count

## Premise

- **A CSV file whose rows have different column counts is not expected.
  Sep's settings are not changed to relax the column-count check and display
  such a file.**

The decisions below are based on this premise.

---

## ADR 1: Schema Inference

### Context

The CSV schema is inferred in two phases: an initial scan of the first 200
rows, and a background scan of the remaining rows that refines the schema.
Either phase can reach a row whose column count differs from the header's.

### Decision

- **Initial scan:** If a mismatched row is found in the first 200 rows, the
  scan fails and an error view is shown.
- **Background scan:** If a mismatched row is found, the scan ends at that row
  and the schema is finalized from the rows before it. The task also ends at
  that point.
- Only the column-count exception ends the scan this way. For an I/O error, the
  background task fails, but the schema from the initial scan is kept.

### Rationale

- A mismatched column count is not an expected state, so the scan ends at that
  mismatched row.

### Rejected Alternatives

- **Skip the mismatched row and continue inference with the rows after it.**
  This is possible, but for now it is judged unnecessary to spend
  implementation cost on it just for schema inference.

---

## ADR 2: Table Display

### Context

Sep also checks the column count when rows are read for table display.
However, it compares each row with the row where the read started, not with
the header.

### Decision

- A row whose column count differs from the header's is treated as an error.
  This includes rows that are uniformly shorter, uniformly longer, or mixed.

### Rationale

- A mismatched column count is not an expected state, so table display also
  treats it as an error.

### Rejected Alternatives

- **Adjust rows with fewer or more columns and display them.** This was
  considered, but it was not adopted because there is no need to spend
  implementation cost on it.

---

## ADR 3: Display Error

### Context

A column-count mismatch is detected while the table is being drawn, inside the
process that gets a cell's value.

### 1. Switch to the error view

#### Decision

- If a row has a mismatched column count, the view switches to the error view.
  The switch is made by the table source running a switch action passed to it.

#### Rationale

- The mismatch is detected in a getter during drawing. Letting the exception
  propagate would crash the application, so it is handled as a switch to the
  error view.
- The table source is responsible only for providing data and should not hold
  what the screen switch does, so the switch action is passed in from outside.

### 2. Treat cells as empty and stop reading

#### Decision

- When a cell is requested, it is treated as an empty cell, and no further rows
  are read from the file.

#### Rationale

- Since the view switches to the error view, later reads are no longer needed.

### 3. Apply the switch after drawing, and discard it if the view has been replaced

#### Decision

- The view is not replaced during drawing. The switch is applied on the UI
  thread after drawing completes.
- If the view that hit the error has already been replaced, the error is
  discarded.

#### Rationale

- While drawing is in progress, that view is still being drawn, so the
  replacement waits for drawing to complete.
- A view that has been replaced is no longer used, so reporting its error is
  not needed.

---

## ADR 4: Filtering

### Context

The filter index is built by reading rows in the same way as table display.
The lazy filter scan is shared by CSV and JSON Lines.

### 1. Treat a column-count mismatch as an exception

#### Decision

- When the CSV filter index is built, a column-count mismatch is treated as an
  exception, as in table display. Mismatched rows are not padded, truncated,
  or skipped.

#### Rationale

- A mismatched column count is not an expected state, so filtering also treats
  it as an error.

### 2. Show a failed scan in the error view

#### Decision

- If the filter scan fails, the view switches to the error view. By the same
  rule as in ADR 3, the error is applied only if the view whose scan failed is
  still the current view.
- A cancelled scan is not reported as an error.

#### Rationale

- If a failure were silently discarded, an incomplete filter result would keep
  being displayed.

### 3. Treat JSON Lines the same way

#### Decision

- A failed JSON Lines filter scan is also shown in the error view in the same
  way.

#### Rationale

- JSON Lines tables use the same lazy filter scan.

---

## ADR 5: CLI

### Context

The CLI reads CSV files separately from the TUI.

### Decision

- The current behavior is kept: a column-count mismatch is an error.

### Rationale

- A mismatched column count is not an expected state, so the CLI also treats
  it as an error. The current behavior already does this, so it is not
  changed.
