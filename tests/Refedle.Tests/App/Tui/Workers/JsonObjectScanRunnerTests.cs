using AwesomeAssertions;
using Refedle.App.Tui.Workers;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class JsonObjectScanRunnerTests : IDisposable
{
    private readonly string _testFilePath;

    public JsonObjectScanRunnerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task RunAsync_WithJsonObjectFile_ReturnsTopLevelEntries()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"name\":\"Alice\",\"age\":30}");

        // Act
        var entries = await JsonObjectScanRunner.RunAsync(_testFilePath, CancellationToken.None);

        // Assert
        entries.Select(e => e.Key).Should().Equal("name", "age");
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"name\":\"Alice\"}");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await JsonObjectScanRunner.RunAsync(_testFilePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithNonExistentFilePath_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid()}.json");

        // Act
        var act = async () => await JsonObjectScanRunner.RunAsync(nonExistentPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
