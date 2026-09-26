# Design: Display Column Types in Table View Headers

## Requirements

Currently, the TUI table view for both CSV and JSON Lines files shows only the column name
in the header row. Users have no way to understand the data schema (e.g., whether a column
is an integer, a floating-point number, or a timestamp) without inspecting the data cells
themselves.

The goal is to display the inferred column type alongside the column name in the header,
for both CSV table view and JSON Lines table mode view, so that users can understand the
schema at a glance.

### Scope

- **In scope**: TUI table view headers (both CSV and JSONL table mode)
- **Out of scope**: Output file headers (modifying output CSV or JSONL files), CLI batch mode

---

## Proposed Header Format

Column headers will be formatted as:

```
<column_name> (<type_label>)
```

Examples:

| Column Name | ColumnType    | Display Header          |
|-------------|---------------|-------------------------|
| `name`      | Text          | `name (text)`           |
| `age`       | WholeNumber   | `age (number)`          |
| `price`     | FloatingPoint | `price (float)`         |
| `active`    | Boolean       | `active (bool)`         |
| `created`   | Timestamp     | `created (datetime)`    |
| `meta`      | JsonObject    | `meta (object)`         |
| `tags`      | JsonArray     | `tags (array)`          |

Type labels are lowercase abbreviations chosen to be concise and familiar to developers,
while remaining readable in narrow TUI column widths.

### Type Label Mapping

| `ColumnType` enum value | Display label |
|------------------------|---------------|
| `Text`                 | `text`        |
| `WholeNumber`          | `number`      |
| `FloatingPoint`        | `float`       |
| `Boolean`              | `bool`        |
| `Timestamp`            | `datetime`    |
| `JsonObject`           | `object`      |
| `JsonArray`            | `array`       |

---

## Affected Components

### New File

#### `src/App/Tui/Ui/Views/ColumnTypeLabel.cs`

A small internal static helper that converts a `ColumnType` enum value to its display label.

```csharp
internal static class ColumnTypeLabel
{
    internal static string ToLabel(ColumnType type) => type switch
    {
        ColumnType.Text          => "text",
        ColumnType.WholeNumber   => "number",
        ColumnType.FloatingPoint => "float",
        ColumnType.Boolean       => "bool",
        ColumnType.Timestamp     => "datetime",
        ColumnType.JsonObject    => "object",
        ColumnType.JsonArray     => "array",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unexpected ColumnType: {type}"),
    };
}
```

The fallback arm (`_`) throws `ArgumentOutOfRangeException` to catch unhandled enum values at development time.

---

### Modified Files

#### `src/App/Tui/Ui/Views/VirtualTableSource.cs`

**Current** (line 20):
```csharp
_columnNames = [.. _schema.Columns.Select(c => c.Name)];
```

**Proposed**:
```csharp
_columnNames = [.. _schema.Columns.Select(c => $"{c.Name} ({ColumnTypeLabel.ToLabel(c.Type)})")];
```

No other changes required. `VirtualTableSource` does not use column names for data lookup.

---

#### `src/App/Tui/Ui/Views/JsonLinesTableSource.cs`

**`_columnNames`** (display, shown in table header) — include type label.

**`_columnNameUtf8`** (lookup key, passed to `CellExtractor.ExtractCell`) — must remain the
plain column name, because `CellExtractor` uses it as a JSON object key to read cell values.
Changing it would break cell data extraction.

**Current** `BuildColumnNames` (line 85–86):
```csharp
private static string[] BuildColumnNames(TableSchema schema) =>
    [.. schema.Columns.Select(c => c.Name)];
```

**Proposed**:
```csharp
private static string[] BuildColumnNames(TableSchema schema) =>
    [.. schema.Columns.Select(c => $"{c.Name} ({ColumnTypeLabel.ToLabel(c.Type)})")];
```

`BuildColumnNamesUtf8` is left unchanged — it encodes the plain column name for JSON field
lookup and must not include the type label.

---

## Data Flow After Change

```
TableSchema (with ColumnSchema.Name + ColumnSchema.Type)
    │
    ├─ VirtualTableSource
    │   ├─ _columnNames  → "name (text)", "age (int)", ...   [display]
    │   └─ cell lookup via DataRowCache (index-based, unaffected)
    │
    └─ JsonLinesTableSource
        ├─ _columnNames      → "name (text)", "age (int)", ...   [display]
        └─ _columnNameUtf8   → b"name", b"age", ...              [JSON key lookup, unchanged]
```

---

## Unit Tests

### New test file: `src/Tests/App/ColumnTypeLabelTests.cs`

Verify the label conversion for all known `ColumnType` values:

- `Text` → `"text"`
- `WholeNumber` → `"number"`
- `FloatingPoint` → `"float"`
- `Boolean` → `"bool"`
- `Timestamp` → `"datetime"`
- `JsonObject` → `"object"`
- `JsonArray` → `"array"`

### Modified / new test for `JsonLinesTableSource`

Verify that after construction:
- `ColumnNames` includes the type label (e.g., `"name (text)"`)
- The internal UTF-8 key array is not affected (verifiable indirectly by confirming
  that cell extraction still works correctly with plain-name lookup)

### Modified / new test for `VirtualTableSource`

Verify that `ColumnNames` includes the type label for all columns.

---

## Non-Goals

- No change to CLI batch output (CSV output files, JSONL output files).
- No change to `BatchOutputSchema` or `ActionApplier`.
- No change to `LazyTransformer` (the recipe transformation preview pipeline).
- The type label is not localised; English-only labels are sufficient for this tool.
