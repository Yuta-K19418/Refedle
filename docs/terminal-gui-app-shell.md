# Implementation Plan: Terminal.Gui v2.0 Application Shell

## Overview

Implement the Terminal.Gui v2.0 application shell as the foundation for Refedle's TUI interface. This is the first step in a three-phase implementation (Application Shell → VirtualGridView → CSV Table Mode).

## Background

- **Current State**: `src/App/Program.cs` contains only "Hello, World!" placeholder
- **Existing Infrastructure**: Engine layer has `TableSchema`, `ColumnSchema`, `CsvRowIndexer`
- **Goal**: Create a minimal but complete TUI application shell that can be extended with views in subsequent issues

## Objectives

1. Initialize Terminal.Gui v2.0 application lifecycle
2. Create main window with menu structure
3. Implement basic keyboard shortcuts (Ctrl+Q to quit)
4. Add file selection dialog integration
5. Display placeholder content (to be replaced by VirtualGridView in next phase)
6. Ensure Native AOT compatibility and zero-warning build

## Implementation Details

### 1. Package Dependencies

**Add to `src/App/Refedle.App.csproj`:**
```xml
<PackageReference Include="Terminal.Gui" Version="2.0.0-develop.4828" />
```

**Rationale:**
- Use latest develop version for newest features and bug fixes
- Terminal.Gui v2.0 supports Native AOT trimming
- Suppress IL2026, IL3050, IL3053 warnings from Terminal.Gui with `<NoWarn>` in .csproj

### 2. Application Structure

**File Organization:**
```
src/App/
├── Program.cs                    (MODIFY: Entry point)
├── TuiApplication.cs             (CREATE: App factory)
├── MainWindow.cs                 (CREATE: Main window with menu/status bar)
├── AppState.cs                   (CREATE: State management)
├── ViewMode.cs                   (CREATE: View mode enum)
└── Views/
    ├── FileSelectionView.cs      (CREATE: File picker)
    └── PlaceholderView.cs        (CREATE: Temporary content view)
```

### 3. Core Components

#### TuiApplication.cs
```csharp
internal static class TuiApplication
{
    public static (IApplication app, MainWindow mainWindow) Create()
    {
        var app = Application.Create();
        var state = new AppState();
        var mainWindow = new MainWindow(app, state);

        return (app, mainWindow);
    }
}
```

**Key Features:**
- Static factory method that returns IApplication and MainWindow tuple
- Caller is responsible for disposing both returned instances
- Simplifies resource management by delegating to caller

#### MainWindow.cs
```csharp
internal sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private View? _currentContentView;

    public MainWindow(IApplication app, AppState state)
    {
        // Initialize menu bar, status bar, and initial content view
    }
}
```

**Key Features:**
- Inherits from Terminal.Gui's `Window`
- Manages menu bar with File → Open, File → Exit
- Status bar with keyboard shortcuts (Ctrl+O: Open, Ctrl+X: Quit)
- Dynamic content view switching
- Proper disposal of child views

#### AppState.cs
```csharp
internal sealed class AppState
{
    public string CurrentFilePath { get; set; } = string.Empty;

    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;
}
```

**Key Features:**
- Simple state holder without complex logic
- Mutable properties for ease of use in single-threaded TUI context
- Extensible for additional state (schema, indexer, etc.)
- Thread-safety will be added when background processing is introduced

#### ViewMode.cs
```csharp
internal enum ViewMode
{
    FileSelection,
    PlaceholderView  // Will be replaced by CsvTable, JsonTable in future issues
}
```

#### FileSelectionView.cs
- Static factory method `Create()` returns a new instance
- Displays centered welcome message and instructions
- Shows "Press Ctrl+O to open a file"
- Inherits from Terminal.Gui's `View`

#### PlaceholderView.cs
- Static factory method `Create(AppState state)` returns a new instance
- Displays "File loaded: {filePath}"
- Instructions: "Press Ctrl+X to quit or Ctrl+O to open another file"
- To be replaced by VirtualGridView in next phase

### 4. Program.cs Entry Point

```csharp
using Refedle.App.Tui.Ui;

var result = TuiApplication.Create();
using var app = result.app;
using var mainWindow = result.mainWindow;

app.Init();
app.Run(mainWindow);
```

**Key Features:**
- Simple and straightforward entry point
- Proper disposal of both IApplication and MainWindow via `using` declarations
- Calls `app.Init()` before `app.Run()` as required by Terminal.Gui v2.0
- No return value (relies on Terminal.Gui's exception handling)

### 5. Keyboard Shortcuts

- **Ctrl+X**: Quit application
- **Ctrl+O**: Open file dialog
- **Escape**: Cancel dialogs (built into Terminal.Gui)

### 6. Error Handling

- Relies on Terminal.Gui's built-in exception handling
- Proper resource disposal via `using` declarations prevents resource leaks
- OpenDialog cancellation is handled by checking `dialog.Canceled` and `dialog.Path`

## Testing Strategy

### Unit Tests

**Create: `tests/Refedle.Tests/App/`**

1. **TuiApplicationTests.cs**
   - Test state initialization
   - Test proper disposal (verify no exceptions)
   - Mock Terminal.Gui for headless testing (if possible)

2. **AppStateTests.cs**
   - Test property initialization
   - Test state transitions
   - Use xUnit + AwesomeAssertions

**Note:** Terminal.Gui rendering cannot be easily unit tested. Focus on logic/state tests.

### Manual Testing Checklist

1. Launch application in macOS Terminal
2. Verify menu bar displays correctly
3. Press Ctrl+O to open file dialog
4. Select a CSV file
5. Verify PlaceholderView displays file path
6. Press Ctrl+X to quit
7. Verify graceful shutdown with no errors

### No Benchmarks

- TUI initialization is not a hot path
- Focus on correctness and AOT compatibility

## Critical Files

**To Modify:**
- `src/App/Refedle.App.csproj` - Add Terminal.Gui package
- `src/App/Program.cs` - Replace placeholder

**Created:**
- `src/App/Tui/Ui/TuiApplication.cs`
- `src/App/Tui/Ui/MainWindow.cs`
- `src/App/Tui/AppState.cs`
- `src/App/Tui/ViewMode.cs`
- `src/App/Tui/Ui/Views/FileSelectionView.cs`
- `src/App/Tui/Ui/Views/PlaceholderView.cs`

**Not Yet Created (Future Work):**
- `tests/Refedle.Tests/App/Tui/Ui/TuiApplicationTests.cs`
- `tests/Refedle.Tests/App/Tui/AppStateTests.cs`

**Reference Files:**
- `src/Engine/Models/TableSchema.cs` - Model pattern reference
- `src/Engine/IO/CsvRowIndexer.cs` - Result<T> pattern reference

## Success Criteria

- Terminal.Gui application launches successfully on macOS
- File selection dialog works and updates state
- Keyboard shortcuts (Ctrl+X, Ctrl+O) function correctly
- Zero compiler warnings
- Passes `dotnet format` validation
- Native AOT publish succeeds
- Clean shutdown with proper resource disposal
- Unit tests pass for state management logic
