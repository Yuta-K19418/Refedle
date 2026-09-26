using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class ColumnActionHandlerTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    [Fact]
    public void GetAvailableActions_Always_ReturnsAllSixActions()
    {
        // Arrange
        // Act
        var actions = ColumnActionHandler.GetAvailableActions();

        // Assert
        actions.Should().HaveCount(6);
        actions.Should().Contain(["Rename", "Delete", "Cast", "Filter", "Fill", "Format Timestamp"]);
    }

    [Fact]
    public void ExecuteAction_WithUnknownAction_DoesNotCallOnMorphAction()
    {
        // Arrange
        using var app = CreateTestApp();
        var table = new TableSource();
        var actionCalled = false;
        var handler = new ColumnActionHandler(
            app, table, 0,
            _ => "test",
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
        using var app = CreateTestApp();
        var table = new TableSource();
        var handler = new ColumnActionHandler(
            app, table, 0,
            _ => "test",
            _ => { },
            format);
        app.StopAfterFirstIteration = true;

        // Act
        var exception = Record.Exception(() => handler.ExecuteAction(action));

        // Assert
        exception.Should().BeNull();
    }

    private sealed class TableSource : ITableSource
    {
        public int Rows => 10;
        public int Columns => 3;
        public string[] ColumnNames => ["Col1", "Col2", "Col3"];

        public object this[int row, int col]
        {
            get => $"R{row}C{col}";
            set { }
        }

        public static void AddColumn(string _) { }
        public static void AddRow() { }
        public static void RemoveColumn(int _) { }
        public static void RemoveRow(int _) { }
        public static void Clear() { }
    }
}
