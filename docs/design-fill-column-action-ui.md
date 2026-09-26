# Design: FillColumnAction — UI Integration

## Requirements

Expose `FillColumnAction` from the interactive TUI so users can overwrite all values in a
column without editing a YAML recipe manually. The interaction model must be consistent with
the existing Rename / Delete / Cast / Filter actions.

## Changed Files

### New

- `src/App/Tui/Ui/Views/Dialogs/FillColumnDialog.cs` — modal dialog that accepts the fill value

### Modified

| File | Change |
|------|--------|
| `src/App/Tui/Ui/Views/CsvTableView.cs` | Add `Shift+L` key handler → `HandleFillColumn()` |
| `src/App/Tui/Ui/Views/JsonLinesTableView.cs` | Add `Shift+L` key handler → `HandleFillColumn()` |

### Tests

| File | Tests added |
|------|-------------|
| `tests/.../App/Tui/Ui/Views/Dialogs/FillColumnDialogTests.cs` | NEW — see test plan below |

## Key Binding

| Key | Action |
|-----|--------|
| `Shift+L` | Fill column (mnemonic: fi**L**l) |

`Shift+F` is already taken by Filter. All other existing bindings (`Shift+R`, `Shift+D`,
`Shift+C`) are unchanged.

## Implementation Logic

### FillColumnDialog (`src/App/Tui/Ui/Views/Dialogs/FillColumnDialog.cs`)

```
Title: "Fill Column"

Layout:
  Label  "Column: {columnName}"          Y=0
  Label  "Value:"                         Y=2
  TextField (empty, full width)           Y=2  X=right of label + 1

Buttons: [OK] [Cancel]
```

- The OK button is **always enabled** — an empty string is a valid fill value (e.g. clearing
  all cells).
- On OK: set `Value = textField.Text`, `Confirmed = true`, call `App?.RequestStop()`.
- On Cancel: leave `Confirmed = false`, `App?.RequestStop()` is called by the framework's
  default cancel handler.

```csharp
/// <summary>Gets the fill value entered by the user.</summary>
internal string Value { get; private set; } = string.Empty;

/// <summary>Gets a value indicating whether the user confirmed the action.</summary>
internal bool Confirmed { get; private set; }
```

### CsvTableView / JsonLinesTableView

Add one `if` block alongside the existing key handlers:

```csharp
if (key.KeyCode == (KeyCode.L | KeyCode.ShiftMask))
{
    return HandleFillColumn();
}
```

Handler (identical shape to the other handlers):

```csharp
private bool HandleFillColumn()
{
    if (App is null || OnMorphAction is null || Table is null || SelectedColumn < 0)
    {
        return true;
    }

    var columnName = Table.ColumnNames[SelectedColumn];
    using var dialog = new FillColumnDialog(columnName);
    App.Run(dialog);

    if (!dialog.Confirmed)
    {
        return true;
    }

    OnMorphAction(new FillColumnAction { ColumnName = columnName, Value = dialog.Value });
    return true;
}
```

> `JsonLinesTableView` uses a private field `_onMorphAction` instead of the property
> `OnMorphAction`; apply the same field name convention used in that file.

## Test Plan

### FillColumnDialogTests (new file)

| Test method | Scenario |
|-------------|----------|
| `Constructor_SetsTitle_ToFillColumn` | Title is "Fill Column" |
| `Constructor_WithColumnName_ShowsColumnNameInLabel` | Column name label text contains the column name |
| `Value_BeforeInteraction_IsEmptyString` | `Value` defaults to `""` |
| `Confirmed_BeforeInteraction_IsFalse` | `Confirmed` defaults to `false` |

> `CsvTableView` and `JsonLinesTableView` key-handler methods are `private` and depend on
> `Terminal.Gui` application state (`App.Run`), making them unsuitable for unit testing.
> Correctness is covered transitively by `FillColumnDialogTests` (dialog contract) and the
> existing `ActionApplierTests` / `RecordProcessorTests` (engine contract).
