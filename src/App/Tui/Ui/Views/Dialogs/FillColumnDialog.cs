using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.Dialogs;

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

        var columnLabel = new Label
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        var valueLabel = new Label
        {
            Text = "Value:",
            X = 0,
            Y = 2,
        };
        var textField = new TextField
        {
            Text = string.Empty,
            X = Pos.Right(valueLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
        };
        Add(columnLabel, valueLabel, textField);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

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
