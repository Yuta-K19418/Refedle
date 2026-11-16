using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.UI;

public sealed partial class ViewManagerTests
{
    [Fact]
    public void SwitchToCsvTable_IsRowIndexComplete_FollowsIndexerCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var indexer = new DataRowIndexer(CreateTempFile(".csv", "col1\na\nb\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.Csv, indexer, new FilterIndexingRunner(), null);
        harness.ViewManager.SwitchToCsvTable(indexer, CreateItemCountSchema(DataFormat.Csv));
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<CsvTableView>().Which;
        var isCompleteDelegate = view.IsRowIndexComplete;

        // Act
        var beforeBuild = isCompleteDelegate?.Invoke();
        indexer.BuildIndex();
        var afterBuild = isCompleteDelegate?.Invoke();

        // Assert
        isCompleteDelegate.Should().NotBeNull();
        beforeBuild.Should().BeFalse();
        afterBuild.Should().BeTrue();
    }

    [Fact]
    public void SwitchToJsonLinesTableView_IsRowIndexComplete_FollowsIndexerCompletion()
    {
        // Arrange
        using var app = TestAppFactory.Create();
        var indexer = new Refedle.Engine.IO.JsonLines.RowIndexer(CreateTempFile(".jsonl", "{\"col1\": \"a\"}\n"));
        using var harness = new ItemCountHarness(indexer.FilePath, DataFormat.JsonLines, indexer, new FilterIndexingRunner(), null);
        harness.ViewManager.SwitchToJsonLinesTableView(indexer, CreateItemCountSchema(DataFormat.JsonLines));
        var view = harness.ViewManager.GetCurrentView().Should().BeOfType<JsonLinesTableView>().Which;
        var isCompleteDelegate = view.IsRowIndexComplete;

        // Act
        var beforeBuild = isCompleteDelegate?.Invoke();
        indexer.BuildIndex();
        var afterBuild = isCompleteDelegate?.Invoke();

        // Assert
        isCompleteDelegate.Should().NotBeNull();
        beforeBuild.Should().BeFalse();
        afterBuild.Should().BeTrue();
    }
}
