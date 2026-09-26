# Implementation Plan: Virtualized TableView for Lag-Free Scrolling

## Overview

Implement a virtualized `TableView` to enable lag-free scrolling through millions of CSV/JSON records. This is achieved by creating a custom `ITableSource` implementation (`VirtualTableSource`) that integrates with the Engine layer's `CsvDataRowIndexer` and a new `CsvDataRowCache`.

## Background

- **Current State**: `PlaceholderView` displays only the file path after selection.
- **Existing Infrastructure**:
  - Engine layer: `MmapService`, `CsvDataRowIndexer`, `TableSchema`.
  - App layer: `MainWindow`, `AppState`, `IncrementalSchemaScanner`.
- **Goal**: Use `Terminal.Gui`'s built-in `TableView` to create a virtualized grid that renders only visible rows, ensuring high performance with large datasets.

## Objectives

1.  Implement `VirtualTableSource` which acts as a bridge between the data engine and the UI.
2.  Integrate with `CsvDataRowIndexer` and a new `CsvDataRowCache` for efficient, on-demand row access.
3.  Utilize the built-in features of `Terminal.Gui.TableView` for rendering and scrolling.
4.  Display column headers derived from `TableSchema`, ensuring they are always visible.
5.  Ensure minimal memory usage by not loading the entire file into memory.

## Implementation Details

### 1. Architecture

**Component Interaction:**

The `TableView` in the UI layer requests only the visible data from `VirtualTableSource`. `VirtualTableSource` in turn retrieves this data from `CsvDataRowCache`, which intelligently fetches and caches row data from the underlying `CsvDataRowIndexer` and memory-mapped file. The schema itself is provided by the `IncrementalSchemaScanner`.

**Data Flow:**
```
CSV File
   ├─── Used by CsvDataRowIndexer (creates row offsets, runs in background)
   ├─── Used by IncrementalSchemaScanner (infers schema, runs in background)
   └─── Used by CsvDataRowReader (reads actual row data for the cache)
           ↓
      CsvDataRowCache (manages cached rows using CsvDataRowIndexer and CsvDataRowReader)
           ↓
      VirtualTableSource (implements ITableSource, connects cache to UI)
           ↓
      TableView (Terminal.Gui component for display)
```

### 2. Core Components

#### VirtualTableSource.cs
This class is the core of the virtualized solution. It implements the `Terminal.Gui.Views.ITableSource` interface, which `TableView` uses to request data for display.

```csharp
namespace Refedle.App.Tui.Ui.Views;

internal sealed class VirtualTableSource : ITableSource
{
    private readonly CsvDataRowCache _cache;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;

    public VirtualTableSource(CsvDataRowIndexer indexer, TableSchema schema)
    {
        _schema = schema;
        _columnNames = _schema.Columns.Select(c => c.Name).ToArray();
        _cache = new CsvDataRowCache(indexer, _schema.ColumnCount);
    }

    public int Rows => _cache.TotalRows;
    public int Columns => _schema.ColumnCount;
    public string[] ColumnNames => _columnNames;

    public object this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException();
            }

            var rowData = _cache.GetRow(row);

            if (col < rowData.Count)
            {
                var memory = rowData[col];
                return memory.IsEmpty ? string.Empty : new string(memory.Span);
            }

            // Return empty string for columns that might not exist in a ragged CSV row
            return string.Empty;
        }
    }
}
```

**Key Features:**
- **On-Demand Data Access**: The `this[row, col]` indexer is called by `TableView` only for the rows and columns it needs to draw.
- **Robust Data Handling**: The implementation correctly handles "ragged" CSV files (where rows have different numbers of columns) by returning an empty string for missing cells, preventing crashes.
- **Data Caching**: Delegates row retrieval to `CsvDataRowCache`, which minimizes redundant parsing and I/O operations.
- **Decoupling**: Separates the UI (`TableView`) from the data source logic.

### 3. Integration with Engine Layer

#### Update MainWindow.cs
When a file is selected, `MainWindow` orchestrates the initialization of the engine components and sets up the `TableView`. The process is asynchronous to keep the UI responsive.

```csharp
private async Task LoadCsvFileAsync(string filePath)
{
    // 1. Start building the row index in the background.
    var indexer = new CsvDataRowIndexer(filePath);
    _ = Task.Run(indexer.BuildIndex);

    // 2. Create the schema scanner.
    var schemaScanner = new IncrementalSchemaScanner(filePath);

    try
    {
        // 3. Perform a quick initial scan to get a schema for immediate display.
        var schema = await schemaScanner.InitialScanAsync();
        _state.Schema = schema;
        _state.SchemaScanner = schemaScanner; // Store for potential future use.

        // 4. Start the full background scan to refine the schema.
        _ = Task.Run(async () =>
        {
            var refinedSchema = await schemaScanner.StartBackgroundScanAsync(
                schema,
                _state.Cts.Token // Assumes a CancellationTokenSource is managed in AppState
            );
            // In a full implementation, the UI would be notified of this change.
            _state.Schema = refinedSchema;
        });

        // 5. Switch to the TableView using the initial indexer and schema.
        SwitchToTableView(indexer, schema);
    }
    catch (Exception ex)
    {
        ShowError($"Error loading CSV: {ex.Message}");
    }
}

private void SwitchToTableView(CsvDataRowIndexer indexer, TableSchema schema)
{
    // ... dispose existing view ...

    // Create the TableView with the virtual source and style.
    _currentContentView = new TableView
    {
        X = 0, Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        Table = new Views.VirtualTableSource(indexer, schema),
        Style = new TableStyle() { AlwaysShowHeaders = true }, // Keep headers visible
    };
    Add(_currentContentView);
}
```

#### Update AppState.cs
The `AppState` class holds the shared state, including the schema and cancellation tokens.

```csharp
internal sealed class AppState
{
    public string CurrentFilePath { get; set; } = string.Empty;
    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;

    // Engine and schema-related state
    public TableSchema? Schema { get; set; }
    public IncrementalSchemaScanner? SchemaScanner { get; set; }
    public CancellationTokenSource Cts { get; } = new();
}
```

### 4. Keyboard Shortcuts

All standard keyboard navigation is handled automatically by the `Terminal.Gui.TableView` component:
- **Arrow Keys**: Navigate rows and columns.
- **Page Up/Down**: Jump by viewport height.
- **Home/End**: Jump to the first/last row or column.

### 5. Performance Considerations

- **Virtualization**: The `TableView` only requests data for visible cells, so memory usage is independent of file size.
- **Asynchronous Operations**: Indexing and full schema scanning run in the background, so the UI is not blocked when opening large files.
- **Efficient Caching**: `CsvDataRowCache` reduces the cost of accessing frequently viewed rows.
- **Zero-Allocation Parsing**: The underlying `CsvRowReader` uses `ReadOnlySpan<T>` to avoid string allocations during parsing, minimizing GC pressure.

### 6. Testing Strategy

#### Unit Tests

1.  **`tests/Refedle.Tests/App/Tui/Ui/Views/VirtualTableSourceTests.cs`**:
    - Test constructor and property initialization (`Rows`, `Columns`, `ColumnNames`).
    - Test `this[row, col]` indexer with valid and out-of-range inputs.
    - Mock `CsvDataRowCache` to verify that `VirtualTableSource` correctly retrieves data and handles ragged rows.
    - Ensure it converts `ReadOnlyMemory<char>` to `string` correctly.

#### Manual Testing Checklist

1.  Open a small CSV file (< 100 rows): Verify header and data render correctly.
2.  Open a large CSV file (> 1 million rows):
    - Verify the file opens quickly and the table is displayed almost instantly.
    - Verify smooth, lag-free scrolling using arrow keys and Page Up/Down.
    - Scroll to the end of the file to ensure the indexer has completed successfully.
    - Monitor memory usage to confirm it remains low and constant during scrolling.
3.  Test edge cases:
    - Empty CSV file.
    - CSV with only a header.
    - CSV with ragged rows.
    - CSV with various data types to check schema inference.

### 7. Deferred Features

This implementation focuses on high-performance, read-only rendering. The following are out of scope:
- Column morphing (Delete, Rename, Cast).
- Filtering/search.
- Horizontal scrolling for wide tables.
- Column resizing.
- Cell editing.