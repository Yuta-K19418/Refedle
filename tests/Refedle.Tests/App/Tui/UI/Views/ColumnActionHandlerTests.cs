using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Refedle.Tests.App.Tui.UI.Views;

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
        var table = new TableSource(["Col1", "Col2", "Col3"], ["Raw1", "Raw2", "Raw3"]);
        var actionCalled = false;
        var handler = new ColumnActionHandler(
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
        using var app = CreateTestApp();
        var table = new TableSource(["Col1", "Col2", "Col3"], ["Raw1", "Raw2", "Raw3"]);
        var handler = new ColumnActionHandler(
            app, table, 0,
            _ => { },
            format);
        app.StopAfterFirstIteration = true;

        // Act
        var exception = Record.Exception(() => handler.ExecuteAction(action));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ExecuteAction_WhenFillConfirmed_UsesRawColumnNameInMorphAction()
    {
        // Arrange
        using var app = CreateTestApp();
        var table = new TableSource(["amount (number)"], ["amount"]);
        MorphAction? capturedAction = null;
        var handler = new ColumnActionHandler(
            app, table, 0,
            action => capturedAction = action,
            DataFormat.Csv);
        app.Iteration += (_, _) => app.Keyboard.RaiseKeyDownEvent(Key.Enter);

        // Act
        handler.ExecuteAction("Fill");

        // Assert
        capturedAction.Should().BeOfType<FillColumnAction>().Which.ColumnName.Should().Be("amount");
    }

    private sealed class TableSource(string[] columnNames, string[] rawColumnNames) : IExtendedTableSource
    {
        public long TotalRows => 10;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public string[] RawColumnNames => rawColumnNames;
        public object this[long row, int col] => $"R{row}C{col}";

        public static void AddColumn(string _) { }
        public static void AddRow() { }
        public static void RemoveColumn(int _) { }
        public static void RemoveRow(int _) { }
        public static void Clear() { }
    }
}
