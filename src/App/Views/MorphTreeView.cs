using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.App.Views;

/// <summary>
/// Abstract base class for all format-specific tree views.
/// Provides Vim-key navigation (h/j/k/l/g/G/d/u), 't'-key table-mode toggle,
/// Enter-to-toggle expand/collapse, and global-shortcut passthrough guard.
/// </summary>
internal abstract class MorphTreeView : TreeView
{
    private readonly VimKeyTranslator _vimKeys = new();
    private readonly Action _onTableModeToggle;
    private readonly Action<ITreeNode?> _onSelectionChanged;

    protected MorphTreeView(Action onTableModeToggle, Action<ITreeNode?> onSelectionChanged)
    {
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        ArgumentNullException.ThrowIfNull(onSelectionChanged);
        _onTableModeToggle = onTableModeToggle;
        _onSelectionChanged = onSelectionChanged;
        Accepted += OnAccepted;
        // A single subscription covers both vim-key (AdjustSelection) and native arrow-key
        // navigation, since both update SelectedObject and raise SelectionChanged.
        SelectionChanged += (_, _) => _onSelectionChanged(SelectedObject is ITreeNode node ? node : null);
    }

    private void OnAccepted(object? sender, CommandEventArgs e)
    {
        var node = SelectedObject;
        if (node is null)
        {
            return;
        }

        if (IsExpanded(node))
        {
            Collapse(node);
            return;
        }

        Expand(node);
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.T)
        {
            _onTableModeToggle();
            return true;
        }

        var action = _vimKeys.Translate(key.KeyCode);

        if (TrySelectionMove(action))
        {
            return true;
        }

        return action switch
        {
            VimAction.PendingGSequence => true,
            VimAction.MoveLeft => base.OnKeyDown(new Key(KeyCode.CursorLeft)),
            VimAction.MoveRight => base.OnKeyDown(new Key(KeyCode.CursorRight)),
            VimAction.GoToFirst => ConsumeAction(GoToFirst),
            VimAction.GoToEnd => ConsumeAction(GoToEnd),
            _ => HandleNonVimKey(key),
        };
    }

    // Groups the four selection-offset moves so OnKeyDown's dispatch stays under the complexity cap.
    private bool TrySelectionMove(VimAction action)
    {
        var offset = action switch
        {
            VimAction.MoveDown => 1,
            VimAction.MoveUp => -1,
            VimAction.PageDown => Viewport.Height,
            VimAction.PageUp => -Viewport.Height,
            _ => (int?)null,
        };

        if (offset is null)
        {
            return false;
        }

        AdjustSelection(offset: offset.Value, expandSelection: false);
        return true;
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TreeView's incremental search.
        // By returning false, we let these keys bubble up to AppKeyHandler.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode))
        {
            return false;
        }

        return base.OnKeyDown(key);
    }

    private static bool ConsumeAction(Action action)
    {
        action();
        return true;
    }
}
