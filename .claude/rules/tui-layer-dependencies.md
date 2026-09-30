---
paths:
  - "src/App/Tui/**/*.cs"
  - "src/App/Cli/**/*.cs"
  - "src/App/FormatDetector.cs"
  - "src/App/KeyPathFormatter.cs"
---

# TUI Layer Dependencies

The TUI code is split by the thread it runs on. Layers depend in **one direction only**.

## Layout
- **`src/App/Tui/UI`**: UI-thread side. Everything that touches Terminal.Gui types (windows, views, dialogs, key handling, view orchestration such as `ViewManager`)
- **`src/App/Tui/Workers`**: worker-thread side. Code that runs on background threads or owns their lifecycle (`IndexTaskManager`, `Schema/*`)
- **`src/App/Tui` (directly)**: state used by both sides (`AppState`, `ViewMode`, `DrillDownState`, `DrillDownRequest`)
- **`src/App` (directly)**: `FormatDetector` and `KeyPathFormatter`, shared by Tui and Cli
- **`src/App/Cli`**: the CLI

## Dependency Direction
- `Tui/UI` → `Tui/Workers`: OK
- `Tui/Workers` → `Tui/UI`: NG
- `Tui/UI` ↔ `Cli`, `Tui/Workers` ↔ `Cli`: NG in both directions
- `UI` must not call `Task.Run` directly; offload background work through a `Tui/Workers` method instead (e.g. `FullAggregationScanRunner`, `FilterIndexingRunner`)

Shared types may only be referenced from the layers that use them, never the other way around:
- `Tui` shared (`AppState`, `ViewMode`, `DrillDownState`, `DrillDownRequest`) can be referenced from `Tui/UI` and `Tui/Workers`, and may itself reference `App` shared. It must not reference `Tui/UI`, `Tui/Workers`, or `Cli`.
- `App` shared (`FormatDetector`, `KeyPathFormatter`) can be referenced from `Tui/UI`, `Tui/Workers`, `Tui` shared, and `Cli`. It must not reference anything under `Tui` or `Cli`.

- Do NOT add a reference that goes against this, including `using` directives and fully-qualified names
- When `Workers` code must drive the UI, it must not reference `UI`. Accept a callback (e.g. `Action<Action>`) that the UI side passes in, as `BackgroundSchemaRefiner` does with `uiThreadInvoke`

## Placing a New Type
- Touches Terminal.Gui types or must run on the UI thread → `Tui/UI`
- Runs on a worker thread or owns worker lifecycle → `Tui/Workers`
- Used by both sides → `Tui` directly
- Used by both Tui and Cli → `src/App` directly
