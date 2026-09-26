# Design: FormatTimestampAction — TUI Integration

## Overview

`FormatTimestampAction` was implemented in the Engine layer but is not accessible from the
TUI. This document covers the TUI-side changes required to expose it: a new dialog, key
bindings in both table views, and a `LazyTransformer` update so the preview reflects the
reformatted values.

---

## Requirements

- The user can trigger the action via `Shift+T` when a column is selected in either
  `CsvTableView` or `JsonLinesTableView`.
- A dialog prompts for the `TargetFormat` (a .NET date/time format string).
- The preview (`LazyTransformer`) immediately reflects the reformatted timestamp values
  after the action is added to the action stack.
- The dialog and key binding pattern must be consistent with existing actions
  (`FillColumnDialog`, `CastColumnDialog`, etc.).

---

## Key Binding

| Key | Action |
|-----|--------|
| `Shift+T` | Open `FormatTimestampDialog` for the selected column |

`Shift+T` (`KeyCode.T | KeyCode.ShiftMask`) is currently unbound in both table views.
In `JsonLinesTableView`, lowercase `t` is already used for table mode toggle; `Shift+T` is
distinct and does not conflict.

---

## New Types

### `FormatTimestampDialog`

**File**: `src/App/Tui/Ui/Views/Dialogs/FormatTimestampDialog.cs`

Modal dialog that collects a `TargetFormat` string from the user.

```
┌ Format Timestamp ─────────────────────────────┐
│ Column: created_at (Timestamp)                 │
│                                                │
│ Target format: [                             ] │
│                                                │
│              [ OK ]  [ Cancel ]                │
└────────────────────────────────────────────────┘
```

**Public surface:**

| Member | Type | Description |
|--------|------|-------------|
| `TargetFormat` | `string` | The format string entered by the user. Empty string before interaction. |
| `Confirmed` | `bool` | `true` when the user pressed OK or Enter; `false` on Cancel or dismiss. |

Constructor: `FormatTimestampDialog(string columnName)`.

Interaction behaviour:
- Pressing `Enter` in the text field or clicking `OK` sets `Confirmed = true`, captures
  `TargetFormat` from the text field, and calls `App.RequestStop()`.
- Clicking `Cancel` leaves `Confirmed = false` and calls `App.RequestStop()`.
- `OK` / `Enter` with an empty `TargetFormat` is accepted by the dialog; callers guard
  against empty format strings.

---

## Changed Files

### New

| File | Purpose |
|------|---------|
| `src/App/Tui/Ui/Views/Dialogs/FormatTimestampDialog.cs` | New dialog |
| `tests/Refedle.Tests/App/Tui/Ui/Views/Dialogs/FormatTimestampDialogTests.cs` | Dialog unit tests |

### Modified

| File | Change |
|------|--------|
| `src/App/Tui/Ui/Views/CsvTableView.cs` | Add `Shift+T` dispatch + `HandleFormatTimestamp()` |
| `src/App/Tui/Ui/Views/JsonLinesTableView.cs` | Add `Shift+T` dispatch + `HandleFormatTimestamp()` |
| `src/App/Tui/Ui/Views/LazyTransformer.cs` | Handle `FormatTimestampAction` in `BuildTransformedSchema`; propagate `TargetFormat` to `FormatCellValue` |
| `tests/Refedle.Tests/App/Tui/Ui/Views/LazyTransformerTests.cs` | Tests for `FormatTimestampAction` rendering |

---

## Implementation Logic

### `CsvTableView` and `JsonLinesTableView`

Add `Shift+T` handling in `OnKeyDown`:

```csharp
if (key.KeyCode == (KeyCode.T | KeyCode.ShiftMask))
{
    return HandleFormatTimestamp();
}
```

Add `HandleFormatTimestamp()` private method (same pattern as `HandleFillColumn`):

```csharp
private bool HandleFormatTimestamp()
{
    if (App is null || OnMorphAction is null || GetRawColumnName is null
        || Table is null || SelectedColumn < 0)
    {
        return true;
    }

    var displayName = Table.ColumnNames[SelectedColumn];
    var rawName = GetRawColumnName(SelectedColumn);
    using var dialog = new FormatTimestampDialog(displayName);
    App.Run(dialog);

    if (!dialog.Confirmed || string.IsNullOrEmpty(dialog.TargetFormat))
    {
        return true;
    }

    OnMorphAction(new FormatTimestampAction
    {
        ColumnName = rawName,
        TargetFormat = dialog.TargetFormat,
    });
    return true;
}
```

`JsonLinesTableView` uses `_onMorphAction` / `_getRawColumnName` fields instead of
properties, so the method body uses those fields directly.

### `LazyTransformer`

#### `WorkingColumn` — add `TimestampFormat`

```csharp
private sealed record WorkingColumn(
    int SourceIndex,
    string Name,
    ColumnType Type,
    string? FillValue = null,
    string? FormatString = null   // ← new
);
```

#### `BuildTransformedSchema` — handle `FormatTimestampAction`

Add after the `FillColumnAction` arm:

```csharp
if (action is FormatTimestampAction formatTs)
{
    if (!nameToIndex.TryGetValue(formatTs.ColumnName, out var fmtIdx))
    {
        continue;
    }

    working[fmtIdx] = working[fmtIdx] with { FormatString = formatTs.TargetFormat };
    continue;
}
```

Silently skipping when the column is not found is consistent with all other actions.
Type validation is the Engine's responsibility (`ActionApplier`); `LazyTransformer` is
a best-effort preview renderer and does not re-validate column types.

#### Return tuple — add `formatStrings`

`BuildTransformedSchema` returns an additional element:

```
IReadOnlyList<string?> formatStrings
```

Extracted from remaining columns as:

```csharp
remaining.ConvertAll(workingColumn => workingColumn.FormatString)
```

Stored as a new field `_formatStrings`.

#### `FormatCellValue` — accept format string

Change signature from:

```csharp
private static string FormatCellValue(string rawValue, ColumnType targetType)
```

to:

```csharp
private static string FormatCellValue(string rawValue, ColumnType targetType, string? formatString)
```

In the `ColumnType.Timestamp` arm:

```csharp
ColumnType.Timestamp => DateTime.TryParse(rawValue, out var dt)
    ? dt.ToString(formatString ?? "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
    : "<invalid>",
```

The default `"yyyy-MM-dd HH:mm:ss"` preserves existing behaviour when no
`FormatTimestampAction` is present.

The call site in the indexer:

```csharp
return FormatCellValue(rawValue, _columnTypes[col], _formatStrings[col]);
```

---

## Test Cases

### `FormatTimestampDialogTests`

- `Constructor_SetsTitle_ToFormatTimestamp`
- `TargetFormat_BeforeInteraction_IsEmptyString`
- `Confirmed_BeforeInteraction_IsFalse`

### `LazyTransformerTests` additions

- `FormatTimestampAction_OnTimestampColumn_FormatsValueWithTargetFormat`
  - Source: `"2024-01-15T09:30:00"`, `TargetFormat = "yyyy/MM/dd"` → cell value `"2024/01/15"`
- `FormatTimestampAction_OnNonExistentColumn_IsSkipped`
  - Column name not in schema → result unchanged, no exception
- `FormatTimestampAction_AfterCastToTimestamp_FormatsCorrectly`
  - `CastColumnAction` (Text → Timestamp) followed by `FormatTimestampAction` → formats correctly
- `MultipleFormatTimestampActions_LastOneWins`
  - Two `FormatTimestampAction`s on same column → second format applied
- `FormatTimestampAction_WithInvalidTimestampValue_ReturnsInvalidMarker`
  - Source: `"not-a-date"` → cell value `"<invalid>"`

---

## Decision Record

### Why `Shift+T`?

All other morph actions use `Shift+<letter>`. `T` is the natural mnemonic for "Timestamp".
In `JsonLinesTableView`, `t` (lowercase) toggles table/tree mode; `Shift+T` is a separate
key code and does not conflict.

### Why not validate column type in the handler?

The handler does not guard against non-Timestamp columns (unlike `HandleFilterColumn` which
guards for index readiness). Validation at the Engine level (`ActionApplier`) is the single
source of truth; the TUI would need to replicate that logic and keep it in sync. Letting
the Engine reject invalid stacks on export/apply is the existing pattern for type
mismatches.

### Why preserve the default format in `FormatCellValue`?

`LazyTransformer` is a preview renderer. Columns that have never had a
`FormatTimestampAction` applied should continue to render with the existing default
`"yyyy-MM-dd HH:mm:ss"`. Using `timestampFormat ?? "yyyy-MM-dd HH:mm:ss"` avoids
a behaviour change for all existing view configurations.

### Why skip silently on unknown column?

Consistent with every other action in `BuildTransformedSchema`. The column may have been
deleted by an earlier action in the stack; silently skipping prevents cascading errors in
the preview.
