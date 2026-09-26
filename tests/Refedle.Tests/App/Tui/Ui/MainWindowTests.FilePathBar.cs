using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.Ui;
using Refedle.App.Tui.Ui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui;

public sealed partial class MainWindowTests
{
    private static readonly TimeSpan _openFileTimeout = TimeSpan.FromSeconds(15);

    // Opens the file through the real MainWindow's startup-load path (the same handler the
    // Open dialog feeds) on a live loop, and returns the FilePathBar text read on the loop
    // thread once the window has recorded the file as opened.
    private static async Task<string> OpenFileAndReadFilePathBarTextAsync(string filePath)
    {
        using var cts = new CancellationTokenSource(_openFileTimeout);
        var textTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyTcs = new TaskCompletionSource<(IApplication App, AppState State, FilePathBar PathBar)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // RunAsync drives the loop on the calling thread until it stops, so it gets a
        // dedicated worker that also owns creation and disposal of the UI objects.
        var loopTask = Task.Run(() =>
        {
            using var app = CreateTestApp();
            using var state = new AppState();
            using var mainWindow = new MainWindow(app, state);
            var pathBar = mainWindow.SubViews.OfType<FilePathBar>().Single();

            mainWindow.ScheduleStartupLoad(new TuiStartupOptions(InputFile: filePath));
            readyTcs.SetResult((app, state, pathBar));
            return app.RunAsync(mainWindow, cts.Token, errorHandler: null);
        });

        var (loopApp, loopState, loopPathBar) = await readyTcs.Task;
        await WaitUntilFilePathRecordedAsync(loopState, filePath, cts.Token);

        // Invocations run in order, so this runs after the window's own path update.
        loopApp.Invoke(() =>
        {
            textTcs.TrySetResult(loopPathBar.Text);
            loopApp.RequestStop();
        });
        var text = await textTcs.Task.WaitAsync(_openFileTimeout);
        await loopTask.WaitAsync(_openFileTimeout);
        return text;
    }

    private static async Task WaitUntilFilePathRecordedAsync(AppState state, string filePath, CancellationToken ct)
    {
        while (state.CurrentFilePath != filePath)
        {
            await Task.Delay(20, ct);
        }
    }

    [Fact]
    public void Constructor_WithNoFileOpen_ShowsFileMenuAndEmptyPathBarAfterItOnFirstRow()
    {
        // Arrange
        using var app = CreateTestApp();
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
