using Terminal.Gui.Views;

namespace Refedle.App.Tui.Ui.Views.Dialogs;

/// <summary>
/// Modal dialog for confirming column deletion.
/// </summary>
internal sealed class DeleteColumnDialog : Dialog
{
    /// <summary>
    /// Gets a value indicating whether the user confirmed the deletion.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to be deleted, shown in the prompt.</param>
    internal DeleteColumnDialog(string columnName)
    {
        Title = "Delete Column";

        var label = new Label
        {
            Text = $"Delete column '{columnName}'?",
            X = 0,
            Y = 0,
        };
        Add(label);

        var yesButton = new Button { Text = "Yes" };
        var noButton = new Button { Text = "No" };

        yesButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            Confirmed = true;
            App?.RequestStop();
        };

        AddButton(yesButton);
        AddButton(noButton);
    }
}
