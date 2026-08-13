using AwesomeAssertions;
using Refedle.App.Views.Dialogs;
using Refedle.Engine.Models.Actions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Views.Dialogs;

public sealed class FilterColumnDialogTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        var driver = app.Driver;
        driver.Should().NotBeNull();
        driver.SetScreenSize(80, 25);
        return app;
    }

    private static TextField GetTextField(FilterColumnDialog dialog)
        => dialog.SubViews.OfType<TextField>().First();

    private static OptionSelector<ComparisonType> GetComparisonTypeSelector(FilterColumnDialog dialog)
        => dialog.SubViews.OfType<OptionSelector<ComparisonType>>().First();

    [Fact]
    public void Constructor_SetsTitle_ToFilterColumn()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Title.Should().Be("Filter Column");
    }

    [Fact]
    public void SelectedOperator_BeforeInteraction_IsNull()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.SelectedOperator.Should().BeNull();
    }

    [Fact]
    public void Value_BeforeInteraction_IsNull()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Value.Should().BeNull();
    }

    [Fact]
    public void Confirmed_BeforeInteraction_IsFalse()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.Confirmed.Should().BeFalse();
    }

    [Fact]
    public void SelectedComparisonType_BeforeInteraction_IsNull()
    {
        // Arrange
        var columnName = "testColumn";

        // Act
        using var dialog = new FilterColumnDialog(columnName);

        // Assert
        dialog.SelectedComparisonType.Should().BeNull();
    }

    [Fact]
    public void Confirm_WithUnparseableNumberValue_KeepsDialogOpenAndShowsError()
    {
        // Arrange — NaN is not a finite number, so Number validation rejects it
        using var app = CreateTestApp();
        using var dialog = new FilterColumnDialog("Score");
        var textField = GetTextField(dialog);
        var comparisonTypeSelector = GetComparisonTypeSelector(dialog);
        textField.Text = "NaN";
        comparisonTypeSelector.Value = ComparisonType.Number;

        // Act
        textField.InvokeCommand(Command.Accept);

        // Assert — the dialog stays open (not confirmed) and no selection is recorded
        dialog.Confirmed.Should().BeFalse();
        dialog.SelectedComparisonType.Should().BeNull();
        var errorLabel = dialog.SubViews.OfType<Label>().Last();
        errorLabel.Text.Should().NotBeEmpty();
    }

    [Fact]
    public void Confirm_WithValidInput_SetsSelectedComparisonType()
    {
        // Arrange
        using var app = CreateTestApp();
        using var dialog = new FilterColumnDialog("Score");
        var textField = GetTextField(dialog);
        var comparisonTypeSelector = GetComparisonTypeSelector(dialog);
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
