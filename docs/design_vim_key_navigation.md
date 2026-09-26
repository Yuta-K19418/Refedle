# Design: Vim-Like Key Navigation (Issue #102)

## Overview

Add vim-style key bindings (`h`/`j`/`k`/`l`, `gg`, `Shift+G`) as alternatives to arrow key navigation
across all three view types: CSV TableView, JSON Lines TableView, and JSON Lines TreeView.

---

## Requirements

### Key Mappings — Table Views (CSV & JSON Lines)

| Key     | Action              | Terminal.Gui API                    |
|---------|---------------------|-------------------------------------|
| `h`     | Move left (←)       | `ChangeSelectionByOffset(0, -1)`    |
| `j`     | Move down (↓)       | `ChangeSelectionByOffset(1, 0)`     |
| `k`     | Move up (↑)         | `ChangeSelectionByOffset(-1, 0)`    |
| `l`     | Move right (→)      | `ChangeSelectionByOffset(0, 1)`     |
| `gg`    | Jump to first row   | `ChangeSelectionToStartOfTable()`   |
| `Shift+G` | Jump to last row  | `ChangeSelectionToEndOfTable()`     |

### Key Mappings — Tree View (JSON Lines)

| Key     | Action                       | Terminal.Gui API     |
|---------|------------------------------|----------------------|
| `j`     | Move to next node (↓)        | `AdjustSelection(1)` |
| `k`     | Move to previous node (↑)    | `AdjustSelection(-1)`|
| `h`     | Collapse node / move to parent | simulate `CursorLeft` via `base.OnKeyDown(Key.CursorLeft)` |
| `l`     | Expand node                  | simulate `CursorRight` via `base.OnKeyDown(Key.CursorRight)` |
| `gg`    | Jump to first node           | `GoToFirst()`        |
| `Shift+G` | Jump to last node          | `GoToEnd()`          |

### Invariants

- Existing arrow key behavior must remain unchanged.
- `gg` requires two consecutive `g` presses within 1000 ms (matching vim's default `timeoutlen`).
- Any other key pressed between the two `g` presses resets the pending state.
- If the timeout expires and another `g` arrives, it is treated as a new first `g` (pending restarted).

---

## Design

### Key Abstraction: `VimKeyTranslator`

The `gg` double-key sequence requires stateful detection that is shared across all three view classes.
To avoid duplicating this logic and to make it unit-testable, extract a `VimKeyTranslator` class.

```
src/App/Tui/Ui/Views/VimKeyTranslator.cs
```

**Responsibilities:**
- Accepts a `Key` input.
- Tracks whether the previous key was a lone `g` press (`_pendingG`).
- Returns a `VimAction` value indicating the resolved navigation intent.

**`VimAction` enum:**

```csharp
internal enum VimAction
{
    None,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    GoToFirst,   // gg
    GoToEnd,     // Shift+G
}
```

**`VimKeyTranslator` class:**

```csharp
internal sealed class VimKeyTranslator
{
    private const int GSequenceTimeoutMs = 1000; // matches vim's default timeoutlen

    private bool _pendingG;
    private long _pendingGTimestamp; // Stopwatch.GetTimestamp() ticks

    public VimAction Translate(Key key)
    {
        // Shift+G → GoToEnd (checked before lowercase g to avoid ambiguity)
        if (key.KeyCode == KeyCode.G && key.IsShift)
        {
            _pendingG = false;
            return VimAction.GoToEnd;
        }

        // Second g — check timeout
        if (key.KeyCode == KeyCode.G && _pendingG)
        {
            var elapsed = Stopwatch.GetElapsedTime(_pendingGTimestamp);
            _pendingG = false;

            if (elapsed.TotalMilliseconds <= GSequenceTimeoutMs)
                return VimAction.GoToFirst;

            // Timeout exceeded — treat as new first g
            _pendingG = true;
            _pendingGTimestamp = Stopwatch.GetTimestamp();
            return VimAction.None;
        }

        // First g — enter pending state, record timestamp
        if (key.KeyCode == KeyCode.G)
        {
            _pendingG = true;
            _pendingGTimestamp = Stopwatch.GetTimestamp();
            return VimAction.None; // consumed; wait for second g
        }

        // Any other key resets pending state
        _pendingG = false;

        return key.KeyCode switch
        {
            KeyCode.H => VimAction.MoveLeft,
            KeyCode.J => VimAction.MoveDown,
            KeyCode.K => VimAction.MoveUp,
            KeyCode.L => VimAction.MoveRight,
            _         => VimAction.None,
        };
    }
}
```

> **Note on `gg` key consumption:** When the first `g` is pressed, `Translate` returns `VimAction.None`
> and the view's `OnKeyDown` should return `true` (consumed) to prevent the base class from acting on it.
>
> **Note on timeout approach:** This is key-driven (checked only when the next key arrives), not
> timer-driven. If the user presses `g` and waits indefinitely without pressing another key, the
> pending state persists until the next keystroke — matching vim's built-in `gg` behavior.

---

### `CsvTableView` (New File)

Currently the CSV view uses a plain `TableView` instantiated in `MainWindow`. Since `TableView` is a
sealed-friendly but overridable class in Terminal.Gui v2, create a dedicated subclass to house
the vim key logic.

```
src/App/Tui/Ui/Views/CsvTableView.cs
```

```csharp
internal sealed class CsvTableView : TableView
{
    private readonly VimKeyTranslator _vimKeys = new();

    protected override bool OnKeyDown(Key key)
    {
        var action = _vimKeys.Translate(key);

        return action switch
        {
            VimAction.MoveDown  => ConsumeAction(() => ChangeSelectionByOffset(1, 0)),
            VimAction.MoveUp    => ConsumeAction(() => ChangeSelectionByOffset(-1, 0)),
            VimAction.MoveLeft  => ConsumeAction(() => ChangeSelectionByOffset(0, -1)),
            VimAction.MoveRight => ConsumeAction(() => ChangeSelectionByOffset(0, 1)),
            VimAction.GoToFirst => ConsumeAction(ChangeSelectionToStartOfTable),
            VimAction.GoToEnd   => ConsumeAction(ChangeSelectionToEndOfTable),
            VimAction.None when key.KeyCode == KeyCode.G && !key.IsShift
                            => true,   // first 'g' — consumed, waiting for second
            _               => base.OnKeyDown(key),
        };
    }

    private static bool ConsumeAction(Action action)
    {
        action();
        return true;
    }
}
```

**`MainWindow` change:** Replace `new TableView { ... }` with `new CsvTableView { ... }` in
`SwitchToTableView`.

---

### `JsonLinesTableView` (Modified)

Add a `VimKeyTranslator` field and extend the existing `OnKeyDown` override.

```csharp
private readonly VimKeyTranslator _vimKeys = new();

protected override bool OnKeyDown(Key key)
{
    if (key.KeyCode == KeyCode.T)
    {
        _onTableModeToggle();
        return true;
    }

    var action = _vimKeys.Translate(key);

    return action switch
    {
        VimAction.MoveDown  => ConsumeAction(() => ChangeSelectionByOffset(1, 0)),
        VimAction.MoveUp    => ConsumeAction(() => ChangeSelectionByOffset(-1, 0)),
        VimAction.MoveLeft  => ConsumeAction(() => ChangeSelectionByOffset(0, -1)),
        VimAction.MoveRight => ConsumeAction(() => ChangeSelectionByOffset(0, 1)),
        VimAction.GoToFirst => ConsumeAction(ChangeSelectionToStartOfTable),
        VimAction.GoToEnd   => ConsumeAction(ChangeSelectionToEndOfTable),
        VimAction.None when key.KeyCode == KeyCode.G && !key.IsShift
                        => true,
        _               => base.OnKeyDown(key),
    };
}
```

---

### `JsonLinesTreeView` (Modified)

Add a `VimKeyTranslator` field and extend the existing `OnKeyDown` override.

```csharp
private readonly VimKeyTranslator _vimKeys = new();

protected override bool OnKeyDown(Key key)
{
    if (key.KeyCode == KeyCode.T)
    {
        _onTableModeToggle();
        return true;
    }

    var action = _vimKeys.Translate(key);

    return action switch
    {
        VimAction.MoveDown  => ConsumeAction(() => AdjustSelection(1)),
        VimAction.MoveUp    => ConsumeAction(() => AdjustSelection(-1)),
        VimAction.MoveLeft  => base.OnKeyDown(Key.CursorLeft),   // collapse / parent
        VimAction.MoveRight => base.OnKeyDown(Key.CursorRight),  // expand
        VimAction.GoToFirst => ConsumeAction(GoToFirst),
        VimAction.GoToEnd   => ConsumeAction(GoToEnd),
        VimAction.None when key.KeyCode == KeyCode.G && !key.IsShift
                        => true,
        _               => base.OnKeyDown(key),
    };
}
```

---

## Files Changed

| File | Change |
|------|--------|
| `src/App/Tui/Ui/Views/VimKeyTranslator.cs` | **New** — shared key-to-action translator with `gg` state machine |
| `src/App/Tui/Ui/Views/VimAction.cs` | **New** — `VimAction` enum |
| `src/App/Tui/Ui/Views/CsvTableView.cs` | **New** — `TableView` subclass for CSV with vim keys |
| `src/App/Tui/Ui/Views/JsonLinesTableView.cs` | **Modified** — add `VimKeyTranslator` and dispatch in `OnKeyDown` |
| `src/App/Tui/Ui/Views/JsonLinesTreeView.cs` | **Modified** — add `VimKeyTranslator` and dispatch in `OnKeyDown` |
| `src/App/Tui/Ui/MainWindow.cs` | **Modified** — use `CsvTableView` instead of plain `TableView` |
| `tests/.../Views/VimKeyTranslatorTests.cs` | **New** — unit tests for all key translation logic |

---

## Testing Strategy

### Unit Tests: `VimKeyTranslatorTests`

`VimKeyTranslator` contains all the non-trivial logic and is fully decoupled from Terminal.Gui views,
making it straightforwardly unit-testable.

Key test cases:

| Scenario | Input sequence | Expected `VimAction` |
|----------|---------------|----------------------|
| Single `h` | `h` | `MoveLeft` |
| Single `j` | `j` | `MoveDown` |
| Single `k` | `k` | `MoveUp` |
| Single `l` | `l` | `MoveRight` |
| `gg` within timeout | `g` then `g` (≤ 1000 ms) | `None`, then `GoToFirst` |
| `gg` timeout exceeded | `g` then `g` (> 1000 ms) | `None`, then `None` (new pending) |
| `Shift+G` | `G` (shift) | `GoToEnd` |
| Interrupted `g` | `g` then `j` | `None`, then `MoveDown` (pending reset) |
| First `g` consumed | `g` | `None` (pending set) |
| `Shift+G` clears pending | `g` then `G` (shift) | `None`, then `GoToEnd` |

### Integration

Key dispatch in views (`CsvTableView`, `JsonLinesTableView`, `JsonLinesTreeView`) will be verified
manually, as Terminal.Gui view tests require a running application context.

---

## Edge Cases

- **`gg` followed by `Shift+G`:** pending `g` state is cleared → only `GoToEnd` fires.
- **Rapid `ggg`:** first two `g` = `GoToFirst`; third `g` starts a new pending state.
- **`gg` timeout exceeded:** second `g` is treated as a new first `g`; pending restarts with updated timestamp.
- **`g` pressed and left idle:** pending state persists until the next keystroke (key-driven, no background timer).
- **Empty table (0 rows):** `ChangeSelectionToStartOfTable` / `ChangeSelectionToEndOfTable`
  are no-ops when the table is empty; no special guard needed.
