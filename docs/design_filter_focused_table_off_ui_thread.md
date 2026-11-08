# Design: Run the Focused Table Filter Scan Off the UI Thread

## Decision 1: Staged Display

### Context

A focused table is the result of a drill-down, and all of its rows are already in memory. Currently, applying a filter evaluates every row synchronously on the UI thread, so on a large table the UI freezes until the evaluation finishes.

CSV and JSON Lines tables display the rows found so far and do not depend on completion, but they give the user no way to tell whether the table is still being built or is complete. That problem was split off as a separate issue.

### Options Considered

- A. Staged display: show a loading view until a displayable amount of rows (roughly 200) is reached, then show the table, which grows as more rows are found. While the table is being built, show a spinner and a progress bar at the bottom right, and remove them when the scan completes.
- B. Show the table only after the whole evaluation completes, and add a view-specific cancellation and a re-check just before the completion processing.
- C. Go back to synchronous computation on the UI thread.
- D. Accept no further input until the scan finishes.

### Decision

Option A.

The first display is triggered by a dedicated one-shot threshold notification from the row indexer (a `TaskCompletionSource` completed when the matched-row count reaches the threshold). The view manager awaits in two stages: `WhenAny(thresholdReached, scanTask)` to reveal the table, then `scanTask` to finish. If the scan ends below the threshold, the pending wait is cancelled in `finally` so it does not hang. Two alternatives that judge by the scanned side were rejected together: making the batches finer near the threshold, and leaving the first display tied to batch completion. Batches count scanned rows while the threshold counts matched rows, so a trigger on the scanned side cannot judge when the matched count is reached, and the timing stays governed by the scan side, which delays the first display. Making the batches finer only shrinks that delay.

### Rationale

Options B to D all leave the user waiting until everything is finished. Option C freezes the screen during the computation, which is the very problem this work set out to fix. With option D, a large result cannot be used until the scan ends. With option A, the user can start working from the rows found so far, and can also tell whether the table is still being built or is complete.

## Decision 2: Per-Scan Cancellation

### Context

The only cancellation token available is a session token, which switches only when a new file is opened. If the scan ran under that token alone, a scan whose view had been replaced would still run to the end.

### Options Considered

- A. Give each scan its own `CancellationTokenSource`, and cancel it when the view is replaced (filter change, transform action, returning from a drill-down), stopping the worker.
- B. Use only the session token.

### Decision

Option A. The per-scan token source is linked with the application-level token. It is cancelled and disposed when the view is replaced, and also when the owner is disposed.

### Rationale

This makes it possible to stop a scan that is no longer needed even when the view changes within the same file.

## Decision 3: Keep All Rows in Memory

### Context

A focused table's data is held with all rows in memory. If the display is made staged, a form that limits the amount held is also conceivable.

### Options Considered

- A. Keep the current form, holding all rows in memory.
- B. Hold only the first 200 rows, and re-load the rest through an LRU-cache-like mechanism.

### Decision

Option A.

### Rationale

This change is limited to how the display and the cancellation are handled. Option B changes the data model to one that does not hold all rows, which is a much larger change, so it is not done this time.

## Decision 4: UI-Thread Confinement for Scan State

### Decision

Scan session state is mutated only on the UI thread (inside the UI-thread invoke). Synchronous entry points are UI-thread-only; asynchronous entry points may be called from any thread but mutate state only inside the UI-thread invoke. This contract is documented and covered by tests.

### Options Considered

- A. UI-thread confinement with a documented contract (chosen): consistent with the rest of the view manager.
- B. Add locking around session state: the rest of the view manager's state is unsynchronized, so a local lock would not make the whole class safe and would widen the scope.
