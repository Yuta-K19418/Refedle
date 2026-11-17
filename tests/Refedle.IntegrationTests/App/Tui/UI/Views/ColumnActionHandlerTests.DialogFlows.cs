using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.IntegrationTests.App.Tui.UI.Views;

public sealed class ColumnActionHandlerTests
{
    [Fact]
    public async Task ExecuteAction_WhenFillConfirmed_UsesRawColumnNameInMorphAction()
    {
        // Arrange
        IMorphAction? capturedAction = null;
        await using LivePumpTestSession<ColumnActionHandler> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            TableSource table = new(["amount (number)"], ["amount"]);
            ColumnActionHandler handler = new(
                app, table, 0,
                action => capturedAction = action,
                DataFormat.Csv);
            ModalKeys.SendOnceOnTop(app, window, Key.Enter);
            return handler;
        });

        // Act
        await session.InvokeAsync((_, handler) =>
        {
            handler.ExecuteAction("Fill");
            return Task.CompletedTask;
        });

        // Assert
        capturedAction.Should().BeOfType<FillColumnAction>().Which.ColumnName.Should().Be("amount");
    }

    [Fact]
    public async Task ExecuteAction_WhenFilterAndRowIndexIncomplete_ShowsErrorDialogWithoutAction()
    {
        // Arrange
        bool actionCalled = false;
        string? shownTitle = null;
        string? shownBody = null;
        await using LivePumpTestSession<ColumnActionHandler> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            TableSource table = new(["Col1"], ["Col1"]);
            ColumnActionHandler handler = new(
                app, table, 0,
                _ => actionCalled = true,
                DataFormat.Csv,
                () => false);
            CaptureTopDialogThenSend(app, (t, b) => { shownTitle = t; shownBody = b; }, Key.Enter);
            return handler;
        });

        // Act
        await session.InvokeAsync((_, handler) =>
        {
            handler.ExecuteAction("Filter");
            return Task.CompletedTask;
        });

        // Assert
        shownTitle.Should().Be("Filter");
        shownBody.Should().Be("Row index is still being built. Please wait.");
        actionCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAction_WhenFilterAndRowIndexComplete_OpensFilterDialog()
    {
        // Arrange
        string? shownTitle = null;
        await using LivePumpTestSession<ColumnActionHandler> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            TableSource table = new(["Col1"], ["Col1"]);
            ColumnActionHandler handler = new(app, table, 0, _ => { }, DataFormat.Csv, () => true);
            CaptureTopDialogThenSend(app, (t, _) => shownTitle = t, Key.Esc);
            return handler;
        });

        // Act
        await session.InvokeAsync((_, handler) =>
        {
            handler.ExecuteAction("Filter");
            return Task.CompletedTask;
        });

        // Assert
        shownTitle.Should().Be("Filter Column");
    }

    [Fact]
    public async Task ExecuteAction_WhenFilterAndRowIndexDelegateIsNull_OpensFilterDialog()
    {
        // Arrange
        string? shownTitle = null;
        await using LivePumpTestSession<ColumnActionHandler> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            TableSource table = new(["Col1"], ["Col1"]);
            ColumnActionHandler handler = new(app, table, 0, _ => { }, DataFormat.Csv);
            CaptureTopDialogThenSend(app, (t, _) => shownTitle = t, Key.Esc);
            return handler;
        });

        // Act
        await session.InvokeAsync((_, handler) =>
        {
            handler.ExecuteAction("Filter");
            return Task.CompletedTask;
        });

        // Assert
        shownTitle.Should().Be("Filter Column");
    }

    private static void CaptureTopDialogThenSend(
        IApplication app, Action<string, string> capture, Key key)
    {
        void OnIteration(object? sender, EventArgs<IApplication?> args)
        {
            if (app.TopRunnableView is not Dialog top)
            {
                return;
            }

            app.Iteration -= OnIteration;
            capture(top.Title, top.Text);
            app.Keyboard.RaiseKeyDownEvent(key);
        }

        app.Iteration += OnIteration;
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
