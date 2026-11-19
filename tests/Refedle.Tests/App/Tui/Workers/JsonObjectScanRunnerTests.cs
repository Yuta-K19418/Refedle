using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.JsonObject;
using Refedle.TestHelpers.IO;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class JsonObjectScanRunnerTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.ReservePath(".json");

    private readonly string _testFilePath;

    public JsonObjectScanRunnerTests()
    {
        _testFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

    [Fact]
    public async Task RunAsync_WithJsonObjectFile_ReturnsTopLevelEntries()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"name\":\"Alice\",\"age\":30}");

        // Act
        IReadOnlyList<JsonObjectEntry> entries = await JsonObjectScanRunner.RunAsync(_testFilePath, CancellationToken.None);

        // Assert
        entries.Select(e => e.Key).Should().Equal("name", "age");
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testFilePath, "{\"name\":\"Alice\"}");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        Func<Task<IReadOnlyList<JsonObjectEntry>>> act = async () => await JsonObjectScanRunner.RunAsync(_testFilePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithNonExistentFilePath_ThrowsFileNotFoundException()
    {
        // Arrange
        using TempFile tempFile = TempFile.ReservePath(".json");
        string nonExistentPath = tempFile.Path;

        // Act
        Func<Task<IReadOnlyList<JsonObjectEntry>>> act = async () => await JsonObjectScanRunner.RunAsync(nonExistentPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
