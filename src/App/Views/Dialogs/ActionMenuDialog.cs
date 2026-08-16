using System.Collections.ObjectModel;
using System.Diagnostics;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Views.Dialogs;

/// <summary>
/// Modal dialog for context-sensitive action menu.
/// Allows users to discover and execute actions available for the current selection.
/// </summary>
internal sealed class ActionMenuDialog : Dialog
{
    private readonly ListView _listView;
    private readonly Action<string> _onConfirmed;

    /// <summary>
    /// Gets the index of the currently selected item in the list.
    /// </summary>
    internal int SelectedItemIndex => _listView.SelectedItem ?? -1;

    /// <summary>
    /// Simulates a key press on the list view. Used in tests to drive navigation.
    /// </summary>
    internal bool SimulateListKeyDown(Key key) => _listView.NewKeyDownEvent(key);

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionMenuDialog"/> class.
    /// </summary>
    /// <param name="availableActions">List of actions available for the current context.</param>
    /// <param name="onConfirmed">Callback invoked when the user confirms an action selection.</param>
    internal ActionMenuDialog(string[] availableActions, Action<string> onConfirmed)
    {
        ArgumentNullException.ThrowIfNull(availableActions);
        ArgumentNullException.ThrowIfNull(onConfirmed);

        _onConfirmed = onConfirmed;

        Title = "Actions";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(50);
        Height = Dim.Percent(50);

        var collection = new ObservableCollection<string>(availableActions);
        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(collection),
        };

        if (collection.Count > 0)
        {
            _listView.SelectedItem = 0;
        }

        _listView.KeyBindings.Add(Key.J, Command.Down);
        _listView.KeyBindings.Add(Key.K, Command.Up);

        _listView.Accepting += (sender, e) => ExecuteSelectedAction();

        Add(_listView);

        // Add cancel button
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            App?.RequestStop();
        };

        AddButton(cancelButton);
    }

    /// <summary>
    /// Executes the currently selected action and closes the dialog.
    /// </summary>
    private void ExecuteSelectedAction()
    {
        var items = _listView.Source?.ToList();
        if (_listView.SelectedItem is { } idx && items is not null && idx < items.Count)
        {
            if (items[idx] is not string selectedAction)
            {
                throw new UnreachableException("List item must be a string.");
            }

            _onConfirmed(selectedAction);
        }

        App?.RequestStop();
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        var baseKey = (char)(key.KeyCode & KeyCode.CharMask);
        var baseKeyLower = char.ToLowerInvariant(baseKey);

        if (key.KeyCode == KeyCode.Esc || baseKeyLower == 'x')
        {
            App?.RequestStop();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
