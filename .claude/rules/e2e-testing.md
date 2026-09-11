---
paths:
  - "tests/Refedle.E2ETests/**/*.cs"
---

# End-to-End Testing

Rules specific to the E2E test project (`tests/Refedle.E2ETests`). The shared
conventions in [testing.md](testing.md) also apply.

## Purpose & Scope
- E2E tests exist to confirm a feature **works end to end** — that a user can actually use it. They are black-box tests of wiring and integration, not of logic.
  - **CLI**: launch the real `refedle` binary as a child process.
  - **TUI**: drive the real `MainWindow` and its key-handling stack on Terminal.Gui's headless ANSI driver.
- Assert **only on observable results**: exit code, `stdout` / `stderr`, output files on disk, rendered screen text. Never assert on production internal state — neither Engine nor App.
  - **CLI**: interact only through the process — arguments in, exit code / streams / files out.
  - **TUI**: go through the real `MainWindow` and key handling. The harness needs `Refedle.App` types to start the session, but assertions look only at rendered screen content, never at view internals.
- Note: the engine type aliases from the shared `GlobalUsings.cs` are excluded from this project via `<Compile Remove>`. Do not reintroduce them.

## Happy Path Only
- E2E tests cover the **happy path only**. Each one shows that a feature works normally, one flow per test.
- **Error handling, abnormal inputs, edge cases, and input validation are NOT covered by E2E tests.** Verify those in unit tests.  
  Note: this does not mean unit tests may skip happy-path coverage. See the Coverage section in [unit-testing.md](unit-testing.md) for the unit test coverage policy.
- Exhaustive coverage of values and conditions also belongs in unit tests. E2E tests start processes and event loops, so they are slow — keep them minimal, one per flow.

## When to Add an E2E Test
- A new CLI subcommand, or a new option.
- A new morph action that reaches an output file — one per input/output format combination that behaves differently.
- A new TUI interaction (menu action, navigation, dialog flow).

## Directory Placement
- Mirror the **surface under test**, not the production directory layout.
  - `Cli/` — top-level commands (`HelpTests`, `VersionTests`, `DryRunTests`, ...)
    - `Cli/Output/{Csv,Json,JsonLines}/` — `apply` flows grouped by output format
  - `Tui/`
    - `Tui/MainWindow/` — `MainWindow` TUI flows
  - `Helpers/` — shared harness and helpers

## Naming Conventions
Follows the naming conventions in [testing.md](testing.md); the E2E-specific points:

- **Class**: `[Surface]Tests` (e.g. `HelpTests`, `CsvOutputTests`, `MainWindowTests`). Split by action into partial files (e.g. `CsvOutputTests.FilterAction.cs`, `MainWindowTests.FilterColumnAction.cs`).
- **CLI method**: `[Command]_[Input]To[Output]_With[Action]_[ExpectedBehavior]`
  - `Run_CsvToCsv_WithFilterAction_ExitsWithZeroAndWritesOnlyMatchingRows`
  - `Apply_WithDryRunWithoutOutput_PrintsSummaryToStdoutAndExitsZero`
- **TUI method**: `[Trigger]_[Scenario]_[ExpectedRenderedBehavior]`
  - `ActionMenu_FilterColumnOnCsvTable_RendersOnlyMatchingRows`

## Helpers
- The shared harness lives in `tests/Refedle.E2ETests/Helpers/`. Read it before writing a test. Do not reimplement process launching or TUI driving inside a test — extend the harness instead when needed.
- Go through `CliProcess` for CLI flows, `TuiTestHarness` for TUI flows, and `TestDirectory` for temporary input/output files.
- Never launch `dotnet` or a process directly from a test, or operate the Terminal.Gui driver directly.

## Test Data
- Define each test's input data — input file contents, recipe YAML, the specific values under test — inline in the test method.
- Do not extract it into a fixture or helper shared across tests, so a reader can follow a test's intent on its own and a shared change cannot break unrelated tests.
- Sharing *mechanism* is fine (`CliProcess`, `TuiTestHarness`, `TestDirectory`, the standard `apply` argument assembly, ...) — it is the input data that stays local.

## Verifying Results
- The effect of an action (screen redraw, output file being written) appears asynchronously. Do not assert right after an action — wait with the harness polling helpers until the expected state is reached, then verify.
- Fixed `Task.Delay` sleeps are forbidden. Poll instead.

## Resource Cleanup
- External resources you create (temp files, the TUI harness, ...) must be released even when the test fails.
- When disposing more than one (e.g. the harness and `TestDirectory`), wrap them so that one throwing during disposal still releases the other (`try` / `finally`).
