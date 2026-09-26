using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.App.Tui.Ui;
using Refedle.Engine.IO;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui;

public sealed partial class MainWindowTests
{
    private const long OneGb = 1_073_741_824L;

    private sealed class ProgressIndexer(long bytesRead, long fileSize) : IRowIndexer
    {
        public long BytesRead { get; } = bytesRead;
        public long FileSize { get; } = fileSize;
        public string FilePath => "test.csv";
        public long TotalRows => 0;
        public bool IsIndexingCompleted => false;

        public event Action? FirstCheckpointReached
        {
            add { }
            remove { }
        }

        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;

        public void RaiseProgressChanged(long bytesRead, long fileSize) => ProgressChanged?.Invoke(bytesRead, fileSize);

        public void RaiseBuildIndexCompleted() => BuildIndexCompleted?.Invoke();

        public void BuildIndex(CancellationToken ct = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }

    private static Label GetIndexingLabel(MainWindow mainWindow) =>
        mainWindow.SubViews.OfType<Label>().Single(l => l.Text.StartsWith(" Indexing…", StringComparison.Ordinal));

    [Fact]
    public void StartIndexing_WithProgress_ShowsOverlayWithInitialProgressText()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;
        app.Begin(mainWindow);

        // Act
        mainWindow.StartIndexing(new ProgressIndexer(1_288_490_189L, 2_147_483_648L));

        // Assert
        mainWindow.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        GetIndexingLabel(mainWindow).Text.Should().Be(" Indexing… 60%  1.2GB / 2.0GB");
    }

    [Fact]
    public void ProgressChanged_WhenIndexerRaisesProgress_UpdatesOverlayText()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;
        app.Begin(mainWindow);
        var indexer = new ProgressIndexer(0L, OneGb);
        mainWindow.StartIndexing(indexer);

        // Act
        indexer.RaiseProgressChanged(OneGb / 2, OneGb);

        // Assert
        GetIndexingLabel(mainWindow).Text.Should().Be(" Indexing… 50%  512.0MB / 1.0GB");
    }

    [Fact]
    public void StartIndexing_WithProgress_PlacesOverlayOneRowAboveStatusBarOneColumnInsideStatusBarRightEdge()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;
        app.Begin(mainWindow);

        // Act
        mainWindow.StartIndexing(new ProgressIndexer(OneGb, 2 * OneGb));
        app.LayoutAndDraw();

        // Assert
        var statusBar = mainWindow.SubViews.OfType<StatusBar>().Single();
        var spinner = mainWindow.SubViews.OfType<SpinnerView>().Single();
        var label = GetIndexingLabel(mainWindow);
        spinner.Frame.Y.Should().Be(statusBar.Frame.Y - 1);
        label.Frame.Y.Should().Be(statusBar.Frame.Y - 1);
        label.Frame.Right.Should().Be(statusBar.Frame.Right - 1);
    }

    [Fact]
    public void BuildIndexCompleted_WithOverlayShown_RemovesAndDisposesSpinnerAndLabel()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;
        app.Begin(mainWindow);
        var indexer = new ProgressIndexer(OneGb, 2 * OneGb);
        mainWindow.StartIndexing(indexer);
        var spinner = mainWindow.SubViews.OfType<SpinnerView>().Single();
        var label = GetIndexingLabel(mainWindow);
        var spinnerDisposed = false;
        var labelDisposed = false;
        spinner.Disposing += (_, _) => spinnerDisposed = true;
        label.Disposing += (_, _) => labelDisposed = true;

        // Act
        indexer.RaiseBuildIndexCompleted();

        // Assert
        mainWindow.SubViews.Should().NotContain(spinner);
        mainWindow.SubViews.Should().NotContain(label);
        spinnerDisposed.Should().BeTrue();
        labelDisposed.Should().BeTrue();
    }

    [Fact]
    public void StopCurrentIndexing_WithOverlayShown_RemovesSpinnerAndLabel()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        app.StopAfterFirstIteration = true;
        app.Begin(mainWindow);
        mainWindow.StartIndexing(new ProgressIndexer(OneGb, 2 * OneGb));

        // Act
        mainWindow.StopCurrentIndexing();

        // Assert
        mainWindow.SubViews.OfType<SpinnerView>().Should().BeEmpty();
        mainWindow.SubViews.OfType<Label>()
            .Where(l => l.Text.StartsWith(" Indexing…", StringComparison.Ordinal))
            .Should().BeEmpty();
    }

    [Fact]
    public void ProgressChanged_WhenPreviousIndexerNotificationIsQueuedBeforeSwitch_DoesNotOverwriteNewIndexerText()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        var previous = new ProgressIndexer(0L, OneGb);
        var current = new ProgressIndexer(0L, 2 * OneGb);
        mainWindow.StartIndexing(previous);
        previous.RaiseProgressChanged(OneGb / 2, OneGb);
        mainWindow.StartIndexing(current);
        var labelBefore = GetIndexingLabel(mainWindow);
        var textBefore = labelBefore.Text;

        // Act
        app.StopAfterFirstIteration = true;
        app.Run(mainWindow);

        // Assert
        var labelAfter = GetIndexingLabel(mainWindow);
        labelAfter.Should().BeSameAs(labelBefore);
        labelAfter.Text.Should().Be(textBefore);
    }

    [Fact]
    public void BuildIndexCompleted_WhenPreviousIndexerNotificationIsQueuedBeforeSwitch_KeepsNewIndexerOverlayShown()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        var previous = new ProgressIndexer(0L, OneGb);
        var current = new ProgressIndexer(0L, 2 * OneGb);
        mainWindow.StartIndexing(previous);
        previous.RaiseBuildIndexCompleted();
        mainWindow.StartIndexing(current);
        var labelBefore = GetIndexingLabel(mainWindow);

        // Act
        app.StopAfterFirstIteration = true;
        app.Run(mainWindow);

        // Assert
        mainWindow.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        var labelAfter = GetIndexingLabel(mainWindow);
        labelAfter.Should().BeSameAs(labelBefore);
    }

    [Fact]
    public void BuildIndexCompleted_WhenNotificationIsQueuedBeforeStopAndNewIndexerStarted_KeepsNewIndexerOverlayShown()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var mainWindow = new MainWindow(app, state);
        var stopped = new ProgressIndexer(0L, OneGb);
        var current = new ProgressIndexer(0L, 2 * OneGb);
        mainWindow.StartIndexing(stopped);
        stopped.RaiseBuildIndexCompleted();
        mainWindow.StopCurrentIndexing();
        mainWindow.StartIndexing(current);
        var labelBefore = GetIndexingLabel(mainWindow);

        // Act
        app.StopAfterFirstIteration = true;
        app.Run(mainWindow);

        // Assert
        mainWindow.SubViews.OfType<SpinnerView>().Should().ContainSingle();
        var labelAfter = GetIndexingLabel(mainWindow);
        labelAfter.Should().BeSameAs(labelBefore);
    }
}
