using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views.Dialogs;

/// <summary>
/// Modal dialog for renaming a column.
/// Displays the current column name and a text field for the new name.
/// </summary>
internal sealed class RenameColumnDialog : Dialog
{
    /// <summary>
    /// Gets the new column name entered by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal string? NewName { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user confirmed the rename.
    /// <see langword="false"/> if cancelled or the same name was entered.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameColumnDialog"/> class.
    /// </summary>
    /// <param name="currentName">The current column name shown as the pre-filled value.</param>
    internal RenameColumnDialog(string currentName)
    {
        Title = "Rename Column";

        Label currentLabel = new()
        {
            Text = $"Current: {currentName}",
            X = 0,
            Y = 0,
        };
        Label newNameLabel = new()
        {
            Text = "New name:",
            X = 0,
            Y = 2,
        };
        TextField textField = new()
        {
            Text = currentName,
            X = Pos.Right(newNameLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
        };
        Add(currentLabel, newNameLabel, textField);

        Button okButton = new()
        { Text = "OK" };
        Button cancelButton = new()
        { Text = "Cancel" };

        void Confirm()
        {
            string name = textField.Text;
            if (string.IsNullOrEmpty(name) || name == currentName)
            {
                return;
            }

            NewName = name;
            Confirmed = true;
            App?.RequestStop();
        }

        okButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            Confirm();
        };

        textField.Accepting += (sender, e) =>
        {
            e.Handled = true;
            Confirm();
        };

        textField.TextChanging += (sender, e) =>
        {
            okButton.Enabled = !string.IsNullOrEmpty(e.Result);
        };

        AddButton(okButton);
        AddButton(cancelButton);
    }
}
