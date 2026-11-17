using Refedle.Engine;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Tui.UI.Views.Dialogs;

/// <summary>
/// Modal dialog for adding a row-level filter condition on a column.
/// Allows the user to select a <see cref="FilterOperator"/>, a <see cref="ComparisonType"/>,
/// and enter a comparison value.
/// </summary>
internal sealed class FilterColumnDialog : Dialog
{
    /// <summary>
    /// Gets a value indicating whether the user confirmed the filter.
    /// <see langword="false"/> if cancelled.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Gets the operator selected by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal FilterOperator? SelectedOperator { get; private set; }

    /// <summary>
    /// Gets the comparison type selected by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal ComparisonType? SelectedComparisonType { get; private set; }

    /// <summary>
    /// Gets the comparison value entered by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal string? Value { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to filter on.</param>
    internal FilterColumnDialog(string columnName)
    {
        Title = "Filter Column";

        Label colLabel = new()
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        Label operatorLabel = new()
        {
            Text = "Operator:",
            X = 0,
            Y = 2,
        };
        OptionSelector<FilterOperator> selector = new()
        {
            X = Pos.Right(operatorLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
            Value = FilterOperator.Equals,
        };
        selector.EnableAutoSelectAndVimKeys();
        Label comparisonTypeLabel = new()
        {
            Text = "Comparison Type:",
            X = 0,
            Y = Pos.Bottom(selector) + 1,
        };
        OptionSelector<ComparisonType> comparisonTypeSelector = new()
        {
            X = Pos.Right(comparisonTypeLabel) + 1,
            Y = Pos.Bottom(selector) + 1,
            Width = Dim.Fill(),
            Value = ComparisonType.Text,
        };
        comparisonTypeSelector.EnableAutoSelectAndVimKeys();
        Label valueLabel = new()
        {
            Text = "Value:",
            X = 0,
            Y = Pos.Bottom(comparisonTypeSelector) + 1,
        };
        TextField textField = new()
        {
            Text = string.Empty,
            X = Pos.Right(valueLabel) + 1,
            Y = Pos.Bottom(comparisonTypeSelector) + 1,
            Width = Dim.Fill(),
        };
        Label errorLabel = new()
        {
            Text = string.Empty,
            X = 0,
            Y = Pos.Bottom(textField) + 1,
        };
        Add(colLabel, operatorLabel, selector, comparisonTypeLabel, comparisonTypeSelector,
            valueLabel, textField, errorLabel);

        Button okButton = new() { Text = "OK" };
        Button cancelButton = new() { Text = "Cancel" };

        void Confirm()
        {
            if (string.IsNullOrWhiteSpace(textField.Text))
            {
                return;
            }

            // OptionSelector always holds a value, but its Value is typed nullable;
            // bind non-null values here and bail out defensively otherwise.
            if (selector.Value is not FilterOperator op
                || comparisonTypeSelector.Value is not ComparisonType comparisonType)
            {
                return;
            }

            // Clear any stale error from a previous failed attempt before re-validating.
            errorLabel.Text = string.Empty;

            Result validation = FilterAction.Validate(op, comparisonType, textField.Text);
            if (validation.IsFailure)
            {
                errorLabel.Text = validation.Error;
                return;
            }

            SelectedOperator = op;
            SelectedComparisonType = comparisonType;
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

        selector.Accepting += (sender, e) =>
        {
            e.Handled = true;
            comparisonTypeSelector.SetFocus();
        };

        comparisonTypeSelector.Accepting += (sender, e) =>
        {
            e.Handled = true;
            textField.SetFocus();
        };

        AddButton(okButton);
        AddButton(cancelButton);
    }
}
