using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.Dialogs;

/// <summary>
/// Modal dialog for formatting a timestamp column with a custom format string.
/// Allows user to specify a .NET date/time format string to reformat timestamp values.
/// </summary>
internal sealed class FormatTimestampDialog : Dialog
{
    /// <summary>
    /// Gets the target format string entered by user.
    /// </summary>
    internal string TargetFormat { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether user confirmed the action.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatTimestampDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of column to format.</param>
    internal FormatTimestampDialog(string columnName)
    {
        Title = "Format Timestamp";

        var columnLabel = new Label
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        var formatLabel = new Label
        {
            Text = "Target format:",
            X = 0,
            Y = 2,
        };
        var textField = new TextField
        {
            Text = string.Empty,
            X = Pos.Right(formatLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
        };
        Add(columnLabel, formatLabel, textField);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

        void Confirm()
        {
            TargetFormat = textField.Text;
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
