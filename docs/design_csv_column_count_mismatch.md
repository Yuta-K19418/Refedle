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
| Background scan (remaining rows) | The mismatched row is skipped, and the scan continues with the rows after it. |

- The background scan skips a mismatched row because that row would show an
  error anyway as soon as it is displayed (see §3). Until then, the remaining
  rows are still used to refine the schema.
- Only Sep's column-count exception (`InvalidDataException`) is skipped.
- Skipping relies on the observed behavior of Sep (0.17.1): after `MoveNext`
  throws a column-count exception, the next `MoveNext` call continues reading
  from the following row. This holds for both shorter and longer rows,
  consecutive mismatched rows, and a following row that contains a quoted line
  break. Sep's documentation does not guarantee this behavior, so unit tests
  pin it.

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
- The error is shown by switching to the error view, in the same way as an
  error from the initial scan when a file is opened. The error is not shown
  per row or in the status bar.
  - The error is not thrown during drawing, and the view is not replaced
    during drawing. The error is reported once and applied on the UI thread
    after the current draw completes.
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

---

## 6. I/O Errors During Enumeration

- `DataRowReader` provides lazy enumeration APIs that the CSV schema scanner
  uses for both the initial and background scans, as the JSON Lines scanner
  does with `RowReader.EnumerateLines`.
- An I/O error (`IOException` or `UnauthorizedAccessException`) that occurs
  while rows are enumerated is not caught. It propagates to the caller.
  - During the initial scan, opening the file shows an error.
  - During the background scan, the scan task fails and the schema from the
    initial scan is kept.
- This differs from `ReadRows`, which returns an empty result on an I/O error.
  Catching the error and ending the enumeration instead would silently publish
  a schema inferred from part of the file as if it were complete.
