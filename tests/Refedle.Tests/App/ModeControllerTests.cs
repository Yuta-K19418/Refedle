using System.Text;
using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.App;

public sealed class ModeControllerTests : IDisposable
{
    private readonly string _jsonlFilePath;

    public ModeControllerTests()
    {
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_jsonlFilePath))
        {
            File.Delete(_jsonlFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new ModeController(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithRowIndexerNull_DoesNothing()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = null
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithUnrelatedMode_DoesNothing()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.CsvTable
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithCachedSchema_ReusesSchema()
    {
        // Arrange
        var cachedSchema = new Refedle.Engine.Models.TableSchema
        {
            Columns = [new Refedle.Engine.Models.ColumnSchema { Name = "id", Type = Refedle.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = Refedle.Engine.Types.DataFormat.JsonLines
        };

        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath),
            Schema = cachedSchema
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        // Should have reused the cached schema
        state.Schema.Should().BeSameAs(cachedSchema);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_ScansSchemaAndSwitchesToTable()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        state.Schema.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTable
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public void DrillDown_JsonObjectWithChildren_PopulatesAllRowsAndSwitchesToFocusedTable(int childCount)
    {
        // Arrange
        var children = string.Join(",", Enumerable.Range(0, childCount).Select(i => $$"""{"id":{{i}}}"""));
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes($"[{children}]");
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: []);
        using var state = new AppState();
        var controller = new ModeController(state);
        var expectedHashValues = Enumerable.Range(0, childCount).Select(i => $"[{i}]");

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.FocusedTable);
        state.DrillDown.Should().BeOfType<DrillDownState>().Which.Rows.Select(r => r.HashValue).Should().Equal(expectedHashValues);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ValidJsonLinesFile_ReturnsSuccessWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(2);
        result.Value.Schema.Columns.Select(c => c.Name).Should().Equal("name");
        state.DrillDown.Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_ScanFails_ReturnsFailureWithoutMutatingState()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("missing", KeyPathSegmentKind.Key)]);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching records found.");
        state.DrillDown.Should().BeNull();
        state.CurrentMode.Should().Be(ViewMode.FileSelection);
    }

    [Fact]
    public void DrillDown_WhenCalledFromJsonObjectTree_CapturesPreviousModeAsJsonObjectTree()
    {
        // Arrange
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes("""[{"id":1}]""");
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: []);
        using var state = new AppState { CurrentMode = ViewMode.JsonObjectTree };
        var controller = new ModeController(state);

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.DrillDown.Should().BeOfType<DrillDownState>()
            .Which.PreviousMode.Should().Be(ViewMode.JsonObjectTree);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WhenCalledFromJsonLinesTree_CapturesPreviousModeAsJsonLinesTree()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
        };
        var controller = new ModeController(state);
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: [new KeyPathSegment("user", KeyPathSegmentKind.Key)]);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreviousMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public void DrillDown_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        JsonRawBytes nodeBytes = Encoding.UTF8.GetBytes("""[{"id":1}]""");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("orders", KeyPathSegmentKind.Key)];
        var request = new SingleDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonObject,
            NodeBytes: nodeBytes,
            KeyPath: keyPath);
        using var state = new AppState();
        var controller = new ModeController(state);

        // Act
        var result = controller.DrillDown(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.DrillDown.Should().BeOfType<DrillDownState>().Which.KeyPath.Should().Equal(keyPath);
    }

    [Fact]
    public async Task FullAggregationDrillDownAsync_WithKeyPathOnRequest_PopulatesDrillDownStateKeyPath()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _jsonlFilePath, "{\"user\":{\"name\":\"Alice\"}}");
        using var state = new AppState { CurrentFilePath = _jsonlFilePath };
        var controller = new ModeController(state);
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        var request = new FullAggregationDrillDownRequest(
            Format: Refedle.Engine.Types.DataFormat.JsonLines,
            KeyPath: keyPath);

        // Act
        var result = await controller.FullAggregationDrillDownAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KeyPath.Should().Equal(keyPath);
    }
}
