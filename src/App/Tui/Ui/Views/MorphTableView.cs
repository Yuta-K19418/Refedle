using Refedle.Engine.Models.Actions;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// Base class for table views that support column morph actions.
/// Provides common implementation for vim-like navigation.
/// </summary>
internal abstract class MorphTableView : TableView
{
    private readonly VimKeyTranslator _vimKeys = new();

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

    /// <summary>
    /// Resolves a column index to the raw (un-labeled) column name for action creation.
    /// When <see langword="null"/>, morphing is disabled (same guard as <see cref="OnMorphAction"/>).
    /// </summary>
    internal Func<int, string>? GetRawColumnName { get; init; }

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
            VimAction.GoToEnd => ConsumeRow(Table.Rows - 1),
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

    private bool ConsumeRow(int row)
    {
        MoveToRow(row);
        return true;
    }

    // Cannot use Command.Start/End as they reset the column to 0 or rightmost.
    // We need to preserve the current column while moving rows.
    private void MoveToRow(int row)
    {
        if (Value is null)
        {
            return;
        }

        SetSelection(col: Value.SelectedCell.X, row: row, extendExistingSelection: false);
        Update();
        SetNeedsDraw();
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TableView's incremental search.
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
