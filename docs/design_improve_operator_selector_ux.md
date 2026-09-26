# Design: Improve Operator Selector UX in Filter Column Dialog

## Requirements

The operator selector and value input in the Filter Column dialog need several UX improvements:

1.  **Select-on-Navigate**: Arrow key navigation should immediately select the focused item in the operator list. Currently, it only moves the highlight and requires an explicit Space or Enter keypress to confirm, which is unintuitive.
2.  **Adaptive Layout**: The "Value" input box must be positioned relative to the operator list (using `Pos.Bottom`) to avoid overlapping when the operator list grows.
3.  **Selection Flow**: After an operator is selected (via Enter, Space, or mouse click), the focus should automatically move to the "Value" text field to streamline input.
4.  **Vim-style Navigation**: Support `j` and `k` keys for navigating up and down the operator list.

## Root Cause

1.  **Selection Logic**: `OptionSelector<FilterOperator>` (from Terminal.Gui v2) uses `CheckBox` subviews with radio style. Its `MoveNext`/`MovePrevious` methods (bound to the arrow keys) only call `SetFocus()` on the target checkbox — they do not update `Value`. `Value` changes only when a checkbox is *activated* (Space/Enter or double-click).
2.  **Static Layout**: The `FilterColumnDialog` currently uses a fixed `Y=4` for the Value input, which causes it to overlap with the operator list (which has 10 items).
3.  **Manual Focus**: Users must manually tab from the operator selector to the text field, adding an extra step to the common workflow.

## Approach

1.  **Auto-selection**: Introduce `AutoSelectOptionSelector<TEnum>` — a thin subclass of `OptionSelector<TEnum>` that overrides `CreateSubViews()`. It subscribes to each `CheckBox` child's `HasFocusChanged` event. When a checkbox gains focus, the selector's `Value` is immediately updated.
2.  **Relative Positioning**: In `FilterColumnDialog.cs`, set `valueLabel.Y` and `textField.Y` to `Pos.Bottom(selector) + 1`. This ensures the input area always stays below the full list of operators.
3.  **Automatic Focus Transition**: Subscribe to the `selector.Accepting` event in `FilterColumnDialog`. When an operator is confirmed (via Enter/Space), call `textField.SetFocus()`.
4.  **Vim Key Support**: Handle the `KeyDown` event on the `OptionSelector` to map `j` to `MoveNext()` and `k` to `MovePrevious()`.

## Files to Change

| File | Change |
|------|--------|
| `src/App/Tui/Ui/Views/AutoSelectOptionSelector.cs` | **New** — `AutoSelectOptionSelector<TEnum>` class with auto-select and Vim key support. |
| `src/App/Tui/Ui/Views/Dialogs/FilterColumnDialog.cs` | Replace `OptionSelector` with `AutoSelectOptionSelector`, update layout to use `Pos.Bottom`, and handle `Accepting` for focus transition. |

## Implementation Notes

- **`AutoSelectOptionSelector<TEnum>`**:
  - Subscribes to each `CheckBox`'s `HasFocusChanged` event inside `CreateSubViews()`.
  - To update `Value`, the handler casts `this` to `SelectorBase` and sets the `int?` property directly. This bypasses `OptionSelector<TEnum>`'s hiding `new` declaration and invokes the real `SelectorBase.Value` setter, which handles validation and UI updates.
  - Overrides key handling to support `j`/`k` for navigation.
- **Layout**: `Pos.Bottom(selector)` is a dynamic layout primitive in Terminal.Gui that recalculates when the selector's height or content changes.
- **Focus Transition**: The `Accepting` event is fired by `OptionSelector` when the user confirms a selection. This is the ideal trigger for moving to the next input field.

## Decision Record

### Rationale

- Subscribing to `HasFocusChanged` on each child `CheckBox` is the narrowest correct hook for arrow-key navigation.
- Using `Pos.Bottom` is the standard "Terminal.Gui way" to handle vertical stacks of variable-sized views.
- Vim keys (`j`/`k`) provide a consistent experience for power users.

### Alternatives Considered

1.  **Override `MoveNext`/`MovePrevious` in a subclass** — These methods are `private` in `SelectorBase`, making them inaccessible. Forking them would require copying internal logic.
2.  **Handle `KeyDown` on `FilterColumnDialog`** — Listening for navigation keys at the dialog level is fragile and could interfere with other key handlers.
3.  **Fixed Height with Scrollbar** — Making the operator selector a fixed height (e.g., 3 rows) with a scrollbar would save space but hide available operators.
4.  **Replace with DropDownList/ComboBox** — A dropdown would solve the space issue but require more clicks to see the options.

### Consequences

- `AutoSelectOptionSelector<TEnum>` is a reusable generic component that can be used in other dialogs like `CastColumnDialog`.
- The `HasFocusChanged` subscription captures `int value` by value in a closure, which is safe and allocation-free after construction.
