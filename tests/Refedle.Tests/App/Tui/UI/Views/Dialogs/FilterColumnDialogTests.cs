using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views.Dialogs;

public sealed class FilterColumnDialogTests
{
    private static TextField GetTextField(FilterColumnDialog dialog)
        => dialog.SubViews.OfType<TextField>().First();

    private static OptionSelector<ComparisonType> GetComparisonTypeSelector(FilterColumnDialog dialog)
        => dialog.SubViews.OfType<OptionSelector<ComparisonType>>().First();

    [Fact]
    public void Constructor_SetsTitle_ToFilterColumn()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FilterColumnDialog dialog = new(columnName);

        // Assert
        dialog.Title.Should().Be("Filter Column");
    }

    [Fact]
    public void SelectedOperator_BeforeInteraction_IsNull()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FilterColumnDialog dialog = new(columnName);

        // Assert
        dialog.SelectedOperator.Should().BeNull();
    }

    [Fact]
    public void Value_BeforeInteraction_IsNull()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FilterColumnDialog dialog = new(columnName);

        // Assert
        dialog.Value.Should().BeNull();
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FilterColumnDialog dialog = new(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void SelectedComparisonType_BeforeInteraction_IsNull()
    {
        // Arrange
        string columnName = "testColumn";

        // Act
        using FilterColumnDialog dialog = new(columnName);

        // Assert
        dialog.SelectedComparisonType.Should().BeNull();
    }

    [Fact]
    public void Confirm_WithUnparseableNumberValue_KeepsDialogOpenAndShowsError()
    {
        // Arrange — NaN is not a finite number, so Number validation rejects it
        using IApplication app = TestAppFactory.Create();
        using FilterColumnDialog dialog = new("Score");
        TextField textField = GetTextField(dialog);
        OptionSelector<ComparisonType> comparisonTypeSelector = GetComparisonTypeSelector(dialog);
        textField.Text = "NaN";
        comparisonTypeSelector.Value = ComparisonType.Number;

        // Act
        textField.InvokeCommand(Command.Accept);

        // Assert — the dialog stays open (not confirmed) and no selection is recorded
        dialog.Confirmed.Should().BeFalse();
        dialog.SelectedComparisonType.Should().BeNull();
        Label errorLabel = dialog.SubViews.OfType<Label>().Last();
        errorLabel.Text.Should().NotBeEmpty();
    }

    [Fact]
    public void Confirm_WithValidInput_SetsSelectedComparisonType()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using FilterColumnDialog dialog = new("Score");
        TextField textField = GetTextField(dialog);
        OptionSelector<ComparisonType> comparisonTypeSelector = GetComparisonTypeSelector(dialog);
        textField.Text = "100";
        comparisonTypeSelector.Value = ComparisonType.Number;

        // Act
        textField.InvokeCommand(Command.Accept);

        // Assert
        dialog.Confirmed.Should().BeTrue();
        dialog.SelectedComparisonType.Should().Be(ComparisonType.Number);
        dialog.Value.Should().Be("100");
    }
}
