using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Views.Dialogs;

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

        var currentLabel = new Label
        {
            Text = $"Current: {currentName}",
            X = 0,
            Y = 0,
        };
        var newNameLabel = new Label
        {
            Text = "New name:",
            X = 0,
            Y = 2,
        };
        var textField = new TextField
        {
            Text = currentName,
            X = Pos.Right(newNameLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
        };
        Add(currentLabel, newNameLabel, textField);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

        void Confirm()
        {
            var name = textField.Text;
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
