using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class ColumnActionHandlerTests
{
    [Fact]
    public void GetAvailableActions_Always_ReturnsAllSixActions()
    {
        // Arrange
        // Act
        string[] actions = ColumnActionHandler.GetAvailableActions();

        // Assert
        actions.Should().HaveCount(6);
        actions.Should().Contain(["Rename", "Delete", "Cast", "Filter", "Fill", "Format Timestamp"]);
    }

    [Fact]
    public void ExecuteAction_WithUnknownAction_DoesNotCallOnMorphAction()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        TenRowTableSource table = new(["Col1", "Col2", "Col3"], ["Raw1", "Raw2", "Raw3"]);
        bool actionCalled = false;
        ColumnActionHandler handler = new(
            app, table, 0,
            _ => actionCalled = true,
            DataFormat.Csv);

        // Act
        handler.ExecuteAction("UnknownAction");

        // Assert
        actionCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Rename", DataFormat.Csv)]
    [InlineData("Delete", DataFormat.Csv)]
    [InlineData("Cast", DataFormat.Csv)]
    [InlineData("Filter", DataFormat.Csv)]
    [InlineData("Fill", DataFormat.Csv)]
    [InlineData("Format Timestamp", DataFormat.Csv)]
    [InlineData("Rename", DataFormat.JsonLines)]
    [InlineData("Delete", DataFormat.JsonLines)]
    [InlineData("Cast", DataFormat.JsonLines)]
    [InlineData("Filter", DataFormat.JsonLines)]
    [InlineData("Fill", DataFormat.JsonLines)]
    [InlineData("Format Timestamp", DataFormat.JsonLines)]
    public void ExecuteAction_WithEachValidAction_DoesNotThrow(string action, DataFormat format)
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        TenRowTableSource table = new(["Col1", "Col2", "Col3"], ["Raw1", "Raw2", "Raw3"]);
        ColumnActionHandler handler = new(
            app, table, 0,
            _ => { },
            format);
        app.StopAfterFirstIteration = true;

        // Act
        Exception exception = Record.Exception(() => handler.ExecuteAction(action));

        // Assert
        exception.Should().BeNull();
    }
}
