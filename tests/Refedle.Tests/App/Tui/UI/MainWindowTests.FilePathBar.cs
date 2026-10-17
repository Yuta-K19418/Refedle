using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class MainWindowTests
{
    // Bundles what the startup-load scenario reads on the loop thread: the state whose
    // CurrentFilePath records the opened file, and the bar whose text is asserted. The
    // MainWindow run by the pump owns and disposes both, so the context only disposes the
    // state; the bar is disposed with its parent window.
    private sealed record StartupLoadContext(AppState State, FilePathBar PathBar) : IDisposable
    {
        public void Dispose()
        {
            State.Dispose();
        }
    }

    // Opens the file through the real MainWindow's startup-load path (the same handler the
    // Open dialog feeds) on a live pump that runs the MainWindow itself, and returns the
    // FilePathBar text read on the loop thread once the window has recorded the file as
    // opened.
    private static async Task<string> OpenFileAndReadFilePathBarTextAsync(string filePath)
    {
        await using var session = await LivePumpTestSession.StartAsync(app =>
        {
            var state = new AppState();
            var mainWindow = new MainWindow(app, state);
            var pathBar = mainWindow.SubViews.OfType<FilePathBar>().Single();
            mainWindow.ScheduleStartupLoad(new TuiStartupOptions(InputFile: filePath));
            return (mainWindow, new StartupLoadContext(state, pathBar));
        });

        // Wait on the loop thread until the startup load has recorded the file path.
        await session.InvokeAsync(async (_, ctx) =>
        {
            while (ctx.State.CurrentFilePath != filePath)
            {
                await Task.Delay(20);
            }
        });

        // Invocations run in order, so this read lands after the path update the load
        // enqueued alongside its CurrentFilePath assignment.
        return await session.InvokeAsync((_, ctx) => Task.FromResult(ctx.PathBar.Text));
    }

    [Fact]
    public void Constructor_WithNoFileOpen_ShowsFileMenuAndEmptyPathBarAfterItOnFirstRow()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;

        // Act
        app.Begin(mainWindow);
        app.LayoutAndDraw();

        // Assert
        var menuBar = mainWindow.SubViews.OfType<MenuBar>().Should().ContainSingle().Which;
        menuBar.SubViews.OfType<MenuBarItem>().Should().ContainSingle().Which.Title.Should().Be("_File");
        var pathBar = mainWindow.SubViews.OfType<FilePathBar>().Should().ContainSingle().Which;
        pathBar.Frame.X.Should().Be(7);
        pathBar.Frame.Y.Should().Be(menuBar.Frame.Y);
        pathBar.Text.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenFile_WithValidJsonLinesFile_ShowsSelectedFileNameInFilePathBar()
    {
        // Arrange
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        await File.WriteAllTextAsync(filePath, "{\"id\":1}\n");

        try
        {
            // Act
            var text = await OpenFileAndReadFilePathBarTextAsync(filePath);

            // Assert
            // The start of a long path may be elided, but the randomly named file always fits.
            var selectedFileName = Path.GetFileName(filePath);
            text.Should().EndWith(selectedFileName);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
