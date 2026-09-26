# IncrementalSchemaScanner Design Document

## Overview

This document describes the design for integrating CSV schema inference with `MainWindow.cs`. The `IncrementalSchemaScanner` class performs an initial schema inference on the first 200 rows, then continues refining the schema in the background for the remaining rows.

## Architecture

```
CSV File Load (LoadCsvFileAsync)
       │
       ├─(1)─→ Create CsvDataRowIndexer (for display)
       │            └─→ Task.Run(BuildIndex)
       │
       ├─(2)─→ Create IncrementalSchemaScanner
       │
       ├─(3)─→ Perform Initial Scan
       │       │
       │       └─→ scanner.InitialScanAsync()
       │            │
       │            ├─→ Read first 200 rows
       │            └─→ Returns initial TableSchema
       │
       ├─(4)─→ Store initial TableSchema and Scanner in AppState
       │
       ├─(5)─→ Start Background Scan (Fire-and-forget)
       │       │
       │       └─→ Task.Run(async () => {
       │             │
       │             ├─→ scanner.StartBackgroundScanAsync(initialSchema, cts.Token)
       │             │     └─→ RefineSchema(row 201 onwards)
       │             │
       │             └─→ Directly update AppState.Schema with final TableSchema
       │           })
       │
       └─(6)─→ Create TableView with VirtualTableSource (using initial schema)
```

## Design Decisions

### CsvDataRowIndexer Not Used for Schema Inference

**Reason**: `CsvDataRowIndexer` builds its index asynchronously, which would add coordination complexity if used for schema inference.

**Solution**: `IncrementalSchemaScanner` reads the CSV file directly using Sep (nietras.SeparatedValues), independent of the indexer.

**Benefits**:
- Simpler design with no dependency on indexer state.
- Schema inference can start immediately without waiting for index construction.
- Clear separation of concerns: indexer for random access, schema scanner for type inference.

## Implementation Details

### IncrementalSchemaScanner Class

**Location**: `src/App/Tui/Workers/Schema/IncrementalSchemaScanner.cs`

**Responsibilities**:
- Read the first 200 rows and perform initial schema inference.
- Continue scanning remaining rows when requested.
- Return the inferred schema to the caller.

**Class Structure**:
```csharp
namespace Refedle.App.Tui.Workers.Schema;

internal sealed class IncrementalSchemaScanner
{
    // ...
    public IncrementalSchemaScanner(string filePath);
    public async Task<TableSchema> InitialScanAsync();
    public Task<TableSchema> StartBackgroundScanAsync(
        TableSchema currentSchema,
        CancellationToken cancellationToken
    );
}
```

### MainWindow Integration

**File**: `src/App/Tui/Ui/MainWindow.cs`

The `LoadCsvFileAsync` method orchestrates the scanning process.

```csharp
private async Task LoadCsvFileAsync(string filePath)
{
    var indexer = new CsvDataRowIndexer(filePath);
    _ = Task.Run(indexer.BuildIndex);

    var schemaScanner = new IncrementalSchemaScanner(filePath);

    try
    {
        // 1. Perform initial scan
        var schema = await schemaScanner.InitialScanAsync();
        
        // 2. Update state with the initial schema and the scanner instance
        _state.Schema = schema;
        _state.SchemaScanner = schemaScanner;

        // 3. Start background scan, which will update the schema in AppState directly
        _ = Task.Run(async () =>
        {
            var refinedSchema = await schemaScanner.StartBackgroundScanAsync(
                schema,
                _state.Cts.Token
            );
            _state.Schema = refinedSchema;
        });

        // 4. Immediately display the table with the initial schema
        SwitchToTableView(indexer, schema);
    }
    catch (Exception ex)
    {
        ShowError($"Error reading CSV file: {ex.Message}");
    }
}
```

### AppState Changes

The `AppState` class is updated to hold references to the schema, the scanner instance, and a `CancellationTokenSource` for background tasks.

```csharp
internal sealed class AppState
{
    public TableSchema? Schema { get; set; }
    public IncrementalSchemaScanner? SchemaScanner { get; set; }
    public CancellationTokenSource Cts { get; set; } = new();
    // ... other properties
}
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| `InitialScanAsync` fails | An exception is caught in `LoadCsvFileAsync`, and an error is displayed to the user. |
| Background scan fails | The `Task.Run` for the background scan completes, but the exception is not handled externally. The app continues to run with the schema state as it was before the failure. |

## Thread Safety Summary

- The `_state.Schema` property can be updated by a background thread. While `TableSchema` is immutable, reads and writes to the reference itself are not guaranteed to be atomic without explicit mechanisms. The current implementation relies on the simplicity of the operation and does not use locks or `Interlocked` operations. This is a potential area for future improvement if race conditions become an issue.

## Potential Concerns and Mitigations

### 1. File Read Twice

**Concern**: Both `CsvDataRowIndexer` (used by the cache for display) and `IncrementalSchemaScanner` (for schema inference) read the file independently.

**Mitigation**: This is a deliberate trade-off for simplicity. Modern OS file caching minimizes the performance impact of repeated reads from disk.

### 2. File Modification During Processing

**Concern**: If the file is modified while being processed, the display and schema could become inconsistent.

**Mitigation**: This is considered acceptable for this type of tool. Users are expected to reload the file if it is modified externally.

### 3. Large File Memory Usage for Initial Scan

**Concern**: Reading the first 200 rows for the initial scan allocates some memory.

**Mitigation**: The memory usage is bounded and predictable (200 rows max). The subsequent background scan processes data in batches and does not accumulate all rows in memory, keeping overall usage low.

### 4. UI Event Handlers and Async Code

**Concern**: The `Open` menu item's action needs to be asynchronous (`async Task`), but `MenuItem` expects a synchronous action.

**Mitigation**: The action is implemented as an `async` lambda (`async () => await ShowFileDialogAsync()`). This is a standard and safe pattern for handling `async` operations in event-driven UI frameworks.