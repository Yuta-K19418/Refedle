using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class FullAggregationScanRunnerTests : IDisposable
{
    private readonly string _testFilePath;

    public FullAggregationScanRunnerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithMatchingRecords_ReturnsScanResult()
    {
        // Arrange
        await File.WriteAllTextAsync(
            _testFilePath,
            "{\"user\":{\"name\":\"Alice\"}}\n{\"user\":{\"name\":\"Bob\"}}\n");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];

        // Act
        var result = await FullAggregationScanRunner.RunAsync(
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
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await FullAggregationScanRunner.RunAsync(
            _testFilePath, DataFormat.JsonLines, keyPath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithJsonObjectFormat_ReturnsScannerFailureResult()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "does-not-need-to-exist.json");
        IReadOnlyList<KeyPathSegment> keyPath = [new KeyPathSegment("user", KeyPathSegmentKind.Key)];

        // Act
        var result = await FullAggregationScanRunner.RunAsync(
            path, DataFormat.JsonObject, keyPath, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("JSON Object format does not support full aggregation.");
    }
}
