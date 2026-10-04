using Refedle.Engine.Models.Actions;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// Base class for table views that support column morph actions.
/// Provides common implementation for vim-like navigation.
/// When the table is a <see cref="PagedTableSource"/>, keeps the selection visible
/// across page boundaries by rewriting the page number and compensating the
/// selection row and scroll offset by the window shift.
/// </summary>
internal abstract class MorphTableView : TableView
{
    private readonly VimKeyTranslator _vimKeys = new();

    /// <summary>
    /// Suppresses selection-change handling while this view is correcting the selection
    /// itself, so the synchronous <see cref="TableView.ValueChanged"/> cannot re-enter.
    /// </summary>
    private bool _isAdjustingSelection;

    /// <summary>
    /// Callback invoked when the user confirms a column morphing action.
    /// <see langword="null"/> means morphing is disabled for this view instance.
    /// </summary>
    internal Action<MorphAction>? OnMorphAction { get; init; }

    /// <summary>
    /// Optional predicate that returns <see langword="true"/> when the row indexer's
    /// <c>BuildIndex</c> has completed. When <see langword="null"/>, the guard is skipped.
    /// The filter action is blocked until this returns <see langword="true"/>.
    /// </summary>
    internal Func<bool>? IsRowIndexComplete { get; init; }

    protected MorphTableView()
    {
        // Type-to-search is disabled: keys must reach AppKeyHandler, and the navigator
        // would search only the rows visible through the paging layer.
        CollectionNavigator = null;

        ValueChanged += HandleSelectionValueChanged;
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (Table is null)
        {
            return base.OnKeyDown(key);
        }

        var action = _vimKeys.Translate(key.KeyCode);

        var command = MapCommand(action);
        if (command.HasValue)
        {
            InvokeCommand(command.Value);
            return true;
        }

        return action switch
        {
            VimAction.GoToFirst => ConsumeRow(0),
            VimAction.GoToEnd => ConsumeRow(LastRowIndex()),
            VimAction.PendingGSequence => true,
            _ => HandleNonVimKey(key),
        };
    }

    private static Command? MapCommand(VimAction action) =>
        action switch
        {
            VimAction.MoveDown => Command.Down,
            VimAction.MoveUp => Command.Up,
            VimAction.MoveLeft => Command.Left,
            VimAction.MoveRight => Command.Right,
            VimAction.PageDown => Command.PageDown,
            VimAction.PageUp => Command.PageUp,
            _ => null,
        };

    // TableView's own end navigation stops at the end of the visible pages, so with
    // paging the true end must come from the paged source's long row count.
    private long LastRowIndex()
    {
        if (Table is null)
        {
            return 0;
        }

        return Table is PagedTableSource paged ? paged.TotalRows - 1 : Table.Rows - 1;
    }

    private bool ConsumeRow(long row)
    {
        if (row < 0)
        {
            return true;
        }

        MoveToRow(row);
        return true;
    }

    // Cannot use Command.Start/End as they reset the column to 0 or rightmost.
    // We need to preserve the current column while moving rows.
    private void MoveToRow(long row)
    {
        var selection = Value;
        if (selection is null)
        {
            return;
        }

        if (Table is not PagedTableSource paged)
        {
            SetSelection(col: selection.SelectedCell.X, row: (int)row, extendExistingSelection: false);
            Update();
            SetNeedsDraw();
            return;
        }

        MoveToAbsoluteRow(paged, row, selection.SelectedCell.X);
    }

    private void MoveToAbsoluteRow(PagedTableSource paged, long absoluteRow, int column)
    {
        paged.SwitchPageFor(absoluteRow);

        _isAdjustingSelection = true;
        try
        {
            RefreshContentSize();
            var localRow = (int)(absoluteRow - paged.WindowStart);
            SetSelection(col: column, row: localRow, extendExistingSelection: false);
            // Put the target row at the bottom edge of the viewport (offset 0 for the first row).
            var visibleRows = Math.Max(1, Viewport.Height - CurrentHeaderHeightVisible());
            RowOffset = Math.Max(0, localRow - visibleRows + 1);
            Update();
        }
        finally
        {
            _isAdjustingSelection = false;
        }

        SetNeedsDraw();
    }

    private void HandleSelectionValueChanged(object? sender, ValueChangedEventArgs<TableSelection?> e)
    {
        if (_isAdjustingSelection || Table is not PagedTableSource paged || e.NewValue is not { } selection)
        {
            return;
        }

        var cell = selection.SelectedCell;
        var pageBeforeSwitch = paged.CurrentPageNumber;
        var windowShift = paged.SwitchPageFor(paged.WindowStart + cell.Y);
        // Entering an adjacent page keeps the window start but grows or shrinks its
        // row span, so every page change still needs the refresh sequence below.
        if (windowShift == 0 && paged.CurrentPageNumber == pageBeforeSwitch)
        {
            return;
        }

        _isAdjustingSelection = true;
        try
        {
            RefreshContentSize();
            SetSelection(col: cell.X, row: (int)(cell.Y - windowShift), extendExistingSelection: false);
            // Keep the same rows on screen after the window slid. Near the window top
            // the offset clamps to 0 and Update() re-centers on the selection.
            RowOffset = (int)Math.Clamp(RowOffset - windowShift, 0L, int.MaxValue);
            Update();
        }
        finally
        {
            _isAdjustingSelection = false;
        }
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TableView key handling.
        // By returning false, we let these keys bubble up to AppKeyHandler.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode))
        {
            return false;
        }

        return base.OnKeyDown(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ValueChanged -= HandleSelectionValueChanged;

            // Idiomatic safe disposal sequence:
            // Unbind data source
            IDisposable? tableToDispose = null;
            if (Table is IDisposable d)
            {
                tableToDispose = d;
            }

            Table = null;

            // Clear selection state (critical to prevent RenderRow crash)
            Value = null;

            // Mark for redraw
            SetNeedsDraw();

            // Dispose data source safely
            tableToDispose?.Dispose();
        }

        base.Dispose(disposing);
    }
}
