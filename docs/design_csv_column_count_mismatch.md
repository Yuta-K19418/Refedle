# Design: Handling CSV Rows with a Mismatched Column Count

This document defines how Refedle treats CSV rows whose column count differs
from the header's, in schema inference, table display, filtering, and the CLI.

---

## 1. Principles

- **A CSV file whose rows do not all have the same number of columns is
  treated as a defect in the file.** Refedle does not repair such rows by
  padding or truncating them.
- **Sep's column-count check is never disabled.** No option such as
  `DisableColCountCheck` is added to `CsvSep`. Sep already checks the column
  count, so turning that check off just to accept mismatched rows would be
  inconsistent.

---

## 2. Schema Inference

| Phase | On a column-count mismatch |
|---|---|
| Initial scan (first 200 rows) | The scan fails, and opening the file shows an error. |
| Background scan (remaining rows) | The scan stops at the mismatched row and the schema is finalized from the rows before it. The scan task does not fail. |

- A mismatched column count is not an expected state, so the scan stops at
  that row. The row would cause an error when it is actually read. Skipping the
  mismatched row and continuing inference with the rows after it is also
  possible, but it is judged unnecessary for now.
- Only the column-count exception (`InvalidDataException`) ends the scan this
  way. An I/O error propagates: the background scan task fails, and the schema
  from the initial scan is kept.

---

## 3. Table Display

- A row whose column count differs from the header's column count is an error.
  This applies whether the rows in the read window are uniformly shorter,
  uniformly longer, or mixed. The rows are never padded or truncated for
  display.
  - The table reads rows in windows that start at a checkpoint without a
    header, so Sep compares each row only with the first row it reads. Rows
    that are uniformly shorter or longer than the header therefore do not
    cause a Sep exception, and Refedle compares every row with the header's
    column count itself.
  - The previous behavior, which padded a window of uniformly shorter rows
    with empty cells and displayed it, is removed.
- If a row has a mismatched column count, Refedle switches to the error view.
  - The cell whose read fails is returned as an empty cell. Until the error
    view is shown, later cell requests also return empty cells without reading
    rows. Letting the exception propagate from the getter could crash the
    application.
  - The table source triggers the switch to the error view and prevents the
    unnecessary file reads that would follow.
  - The view is not replaced during drawing. The error is applied on the UI
    thread after the current draw completes.
  - The error is applied only if the view that hit the error is still the
    current view. An error from a view that has already been replaced is
    discarded.

### 3.1 Rows Skipped Before the Window

- To read a window, the reader starts at a checkpoint and skips the rows
  before the window. A mismatched row in that skipped interval is also an
  error, and the window read fails in the same way as for a mismatch inside the
  window.
- A mismatched row in the skipped interval would also be an error when it is
  displayed, and the file as a whole is still invalid. Ignoring mismatches only
  in the skipped interval would partly accept mismatched rows, against §1.

---

## 4. Filtering

- When the CSV filter index is built, a column-count mismatch is treated as an
  exception, as in table display. The filter does not pad, truncate, or skip
  mismatched rows.
- If the filter scan fails, Refedle switches to the error view
  (`Filter scan failed: ...`). The same rule as in §3 applies: the error is
  applied only if the view whose scan failed is still the current view. A
  cancelled scan is not reported as an error.
- JSON Lines tables use the same lazy filter scan, so a failed JSON Lines
  filter scan is also shown in the error view in the same way.

---

## 5. CLI

- The CLI keeps its current behavior: a column-count mismatch is an error.
