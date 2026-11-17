using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;

namespace Refedle.IntegrationTests.App.Tui.UI;

public sealed partial class ViewManagerTests
{
    [Fact]
    public void SwitchToCsvTable_IsRowIndexComplete_FollowsIndexerCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        DataRowIndexer indexer = new(CreateTempFile(".csv", "col1\na\nb\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.Csv, indexer, new FilterIndexingRunner(), null);
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        CsvTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Which;
        Func<bool>? isCompleteDelegate = view.IsRowIndexComplete;

        // Act
        bool? beforeBuild = isCompleteDelegate?.Invoke();
        indexer.BuildIndex();
        bool? afterBuild = isCompleteDelegate?.Invoke();

        // Assert
        isCompleteDelegate.Should().NotBeNull();
        beforeBuild.Should().BeFalse();
        afterBuild.Should().BeTrue();
    }

    [Fact]
    public void SwitchToJsonLinesTableView_IsRowIndexComplete_FollowsIndexerCompletion()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        RowIndexer indexer = new(CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n"));
        using ItemCountHarness harness = new(indexer.FilePath, DataFormat.JsonLines, indexer, new FilterIndexingRunner(), null);
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        JsonLinesTableView view = harness.ViewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Which;
        Func<bool>? isCompleteDelegate = view.IsRowIndexComplete;

        // Act
        bool? beforeBuild = isCompleteDelegate?.Invoke();
        indexer.BuildIndex();
        bool? afterBuild = isCompleteDelegate?.Invoke();

        // Assert
        isCompleteDelegate.Should().NotBeNull();
        beforeBuild.Should().BeFalse();
        afterBuild.Should().BeTrue();
    }
}
