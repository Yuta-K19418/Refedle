using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views.Dialogs;

/// <summary>
/// Modal dialog for filling a column with a fixed value.
/// Allows user to specify a value to overwrite all cells in a column.
/// </summary>
internal sealed class FillColumnDialog : Dialog
{
    /// <summary>
    /// Gets the fill value entered by the user.
    /// </summary>
    internal string Value { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the user confirmed the action.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FillColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to fill.</param>
    internal FillColumnDialog(string columnName)
    {
        Title = "Fill Column";

        Label columnLabel = new()
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        Label valueLabel = new()
        {
            Text = "Value:",
            X = 0,
            Y = 2,
        };
        TextField textField = new()
        {
            Text = string.Empty,
            X = Pos.Right(valueLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
        };
        Add(columnLabel, valueLabel, textField);

        Button okButton = new() { Text = "OK" };
        Button cancelButton = new() { Text = "Cancel" };

        void Confirm()
        {
            Value = textField.Text;
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

        AddButton(okButton);
        AddButton(cancelButton);
    }
}
