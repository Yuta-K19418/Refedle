using Refedle.Engine.Types;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Views.Dialogs;

/// <summary>
/// Modal dialog for casting a column to a different data type.
/// Displays valid <see cref="ColumnType"/> values for the current data format as radio options.
/// </summary>
internal sealed class CastColumnDialog : Dialog
{
    /// <summary>
    /// Gets the column type selected by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal ColumnType? SelectedType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user confirmed the cast.
    /// <see langword="false"/> if cancelled or the same type was selected.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CastColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to cast.</param>
    /// <param name="currentType">The current column type, pre-selected in the option list.</param>
    /// <param name="format">The data format of the current file.</param>
    internal CastColumnDialog(string columnName, ColumnType currentType, DataFormat format)
    {
        Title = "Cast Column Type";

        var colLabel = new Label
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        var typeLabel = new Label
        {
            Text = $"Current type: {currentType}",
            X = 0,
            Y = 1,
        };
        var selector = new OptionSelector<ColumnType>
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Value = currentType,
        };
        selector.EnableAutoSelectAndVimKeys();

        var errorLabel = new Label
        {
            Text = "Selected type is not supported for this format.",
            X = 0,
            Y = Pos.Bottom(selector) + 1,
            Width = Dim.Fill(),
            Visible = false,
        };

        Add(colLabel, typeLabel, selector, errorLabel);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

        var validTypes = format.GetValidCastTargets();

        void updateErrorState(ColumnType? selected)
        {
            var isValid = selected is not null && validTypes.Contains(selected.Value);
            errorLabel.Visible = !isValid;
            okButton.Enabled = isValid;
        }

        // Initial state
        updateErrorState(currentType);

        foreach (var cb in selector.SubViews.OfType<CheckBox>())
        {
            cb.HasFocusChanged += (_, _) =>
            {
                if (cb.HasFocus)
                {
                    // Value is not yet updated on focus change; confirm() performs final validation.
                    updateErrorState(selector.Value);
                }
            };
        }

        void confirm()
        {
            var selected = selector.Value;
            if (selected == null || selected == currentType)
            {
                return;
            }

            if (!validTypes.Contains(selected.Value))
            {
                return;
            }

            SelectedType = selected;
            Confirmed = true;
            App?.RequestStop();
        }

        okButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            confirm();
        };

        selector.Accepting += (sender, e) =>
        {
            e.Handled = true;
            confirm();
        };

        AddButton(okButton);
        AddButton(cancelButton);
    }
}
