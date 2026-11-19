using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Types;
using Refedle.TestHelpers.IO;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class FullAggregationScanRunnerTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.ReservePath(".jsonl");

    private readonly string _testFilePath;

    public FullAggregationScanRunnerTests()
    {
        _testFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

    [Fact]
    public async Task RunAsync_WithMatchingRecords_ReturnsScanResult()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _testFilePath,
            "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}\n");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];

        // Act
        Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)> result = await FullAggregationScanRunner.RunAsync(
            _testFilePath, DataFormat.JsonLines, keyPath, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"user\":{\"name\":\"Alice\"}}\n");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task<Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)>>> act = async () => await FullAggregationScanRunner.RunAsync(
            _testFilePath, DataFormat.JsonLines, keyPath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithJsonObjectFormat_ReturnsScannerFailureResult()
    {
        // Arrange
        using TempFile tempFile = TempFile.ReservePath(".json");
        string path = tempFile.Path;
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];

        // Act
        Result<(TableSchema schema, IReadOnlyList<FocusedTableRow> rows)> result = await FullAggregationScanRunner.RunAsync(
            path, DataFormat.JsonObject, keyPath, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("JSON Object format does not support full aggregation.");
    }
}
