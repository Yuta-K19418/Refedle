# Design: CLI Options to Specify Input File and Recipe on Startup

## Requirements

- `--file <path>` — loads the given file on startup, bypassing the file selection dialog
- `--recipe <path>` — loads the given recipe on startup, bypassing the load recipe dialog
- Both options can be combined
- Invalid paths produce a clear error to stderr before the TUI launches
- No arguments → existing interactive behavior is preserved

## Affected Files

### New Files

| File | Purpose |
|------|---------|
| `src/App/TuiStartupOptions.cs` | Record holding optional startup arguments for TUI mode |
| `src/App/TuiArgumentParser.cs` | Parses `--file` / `--recipe` from `args[]` in TUI mode |
| `tests/Refedle.Tests/App/Cli/TuiArgumentParserTests.cs` | Unit tests for `TuiArgumentParser` |

### Modified Files

| File | Change |
|------|--------|
| `src/App/Program.cs` | Parse TUI args; validate paths; call `ScheduleStartupLoad()` |
| `src/App/MainWindow.cs` | Add `ScheduleStartupLoad(TuiStartupOptions)` method |
| `src/App/RecipeCommandHandler.cs` | Add `LoadFromPathAsync(string path)` (dialog-free overload) |

## Design Details

### `TuiStartupOptions` Record

```csharp
namespace Refedle.App;

internal sealed record TuiStartupOptions(string? InputFile = null, string? RecipeFile = null)
{
    public bool HasAny => InputFile is not null || RecipeFile is not null;
}
```

Both properties are nullable because all flags are optional in TUI mode. Primary constructor syntax is used per C# coding standards.

---

### `TuiArgumentParser`

Parses args when the first argument is not `apply` (or `update`/`version`). Returns `Result<TuiStartupOptions>` so errors can be reported before the TUI starts.

Accepted flags:

| Flag | Value | Effect |
|------|-------|--------|
| `--file` | `<path>` | Sets `InputFile` |
| `--recipe` | `<path>` | Sets `RecipeFile` |

Unknown flags → `Result.Failure` with a descriptive message.  
Missing value for a known flag → `Result.Failure`.

---

### `Program.cs` Changes

```
if args is ["apply", ..]  → existing CLI path (unchanged)

else:
  parseResult = TuiArgumentParser.Parse(args)
  if parseResult.IsFailure → stderr + exit(1)

  options = parseResult.Value
  if options.InputFile is not null && !File.Exists(options.InputFile)
      → stderr "Error: File not found: <path>" + exit(1)
  if options.RecipeFile is not null && !File.Exists(options.RecipeFile)
      → stderr "Error: Recipe file not found: <path>" + exit(1)

  Create TuiApplication, init, subscribe key handler
  if options.HasAny → mainWindow.ScheduleStartupLoad(options)
  app.Run(mainWindow)
```

Path validation happens **before** `app.Init()` so the TUI never starts if arguments are bad.

---

### `MainWindow.ScheduleStartupLoad(TuiStartupOptions options)`

Queues async startup loading to run once the Terminal.Gui event loop is active.

```
ScheduleStartupLoad(options):
  // Guard: recipe-only without file is not supported
  if (options.InputFile is null) return;

  // Decompose here to ensure null-safety for ExecuteStartupLoadAsync
  _app.Invoke(() => { _ = ExecuteStartupLoadAsync(options.InputFile, options.RecipeFile); })

ExecuteStartupLoadAsync(string inputFile, string? recipeFile):
  await _fileDialogHandler.HandleFileSelectedAsync(inputFile)
  // Guard: file load failed (CurrentFilePath not set)
  if (string.IsNullOrWhiteSpace(_state.CurrentFilePath)) return;
  if (recipeFile is not null)
      await _recipeCommandHandler.LoadFromPathAsync(recipeFile)
```

`HandleFileSelectedAsync` already exists as `internal` and handles format detection, schema scanning, and view switching — no duplication needed.
`_state.CurrentFilePath` is set synchronously inside `HandleFileSelectedAsync` before the first `await`, so it is available by the time `LoadFromPathAsync` is called.

**Note:** Async void is avoided by using fire-and-forget pattern with `_ = ExecuteStartupLoadAsync(options.InputFile, options.RecipeFile)`. `TuiStartupOptions` is decomposed into typed parameters at the call site so the compiler can prove `inputFile` is non-null without using the forbidden `!` operator.

---

### `RecipeCommandHandler.LoadFromPathAsync(string path)`

A dialog-free overload extracted from the existing `LoadAsync()` body. Returns `ValueTask` for better async efficiency.

```
LoadFromPathAsync(path):
  // Precondition: CurrentFilePath must be set (file must be loaded first)
  if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
      throw new InvalidOperationException("Cannot load recipe: no input file is currently loaded.")

  result = await _recipeManager.LoadAsync(path)
  _app.Invoke(() =>
      if result.IsFailure → _viewManager.ShowError(result.Error)
      else
          _state.ActionStack = result.Value.Actions
          _viewManager.RefreshCurrentTableView()
  )
```

The existing `LoadAsync()` is refactored to delegate to `LoadFromPathAsync` to avoid logic duplication.

---

## Interaction Flow (with both options)

```
$ refedle --file data.csv --recipe transforms.yaml

Program.cs
  ├── TuiArgumentParser.Parse → TuiStartupOptions { InputFile, RecipeFile }
  ├── File.Exists checks → OK
  ├── TuiApplication.Create → (app, mainWindow)
  ├── app.Init()
  ├── mainWindow.SubscribeKeyHandler()
  ├── mainWindow.ScheduleStartupLoad(options)   ← queued
  └── app.Run(mainWindow)                        ← event loop starts

  [Event loop first tick]
  └── Invoke fires:
      ├── FileDialogHandler.HandleFileSelectedAsync("data.csv")
      │   ├── Format detection → CSV
      │   ├── _state.CurrentFilePath = "data.csv"
      │   ├── IncrementalSchemaScanner.InitialScanAsync()
      │   └── ViewManager.SwitchToCsvTable(...)
      └── RecipeCommandHandler.LoadFromPathAsync("transforms.yaml")
          ├── RecipeManager.LoadAsync(...)
          └── ViewManager.RefreshCurrentTableView()
```

## Error Scenarios

| Scenario | Behavior |
|----------|----------|
| `--file` path does not exist | stderr error before TUI; exit code 1 |
| `--recipe` path does not exist | stderr error before TUI; exit code 1 |
| `--file` path exists but unsupported format | TUI shows error dialog via `ViewManager.ShowError()` |
| `--recipe` path exists but invalid YAML | TUI shows error dialog via `ViewManager.ShowError()` |
| `--recipe` specified without `--file` | `ScheduleStartupLoad` returns early (InputFile is null guard); treated as if `--recipe` were absent |
| Unknown TUI flag (e.g. `--foo`) | stderr error before TUI; exit code 1 |

## Test Plan

`TuiArgumentParserTests`:
- No args → success with both null
- `--file path.csv` → `InputFile = "path.csv"`, `RecipeFile = null`
- `--recipe recipe.yaml` → `RecipeFile = "recipe.yaml"`, `InputFile = null`
- Both flags → both set correctly
- `--file` without value → failure
- `--recipe` without value → failure
- Unknown flag → failure
- Flag order independence (recipe before file)
- `--file` specified twice (duplicate flag) → failure
- Path with spaces (e.g., `--file "my file.csv"`) → success with correct path
- Empty array input → success with both null
