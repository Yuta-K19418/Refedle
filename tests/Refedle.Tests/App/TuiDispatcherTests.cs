using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli;

namespace Refedle.Tests.App;

public sealed class TuiDispatcherTests
{
    [Fact]
    public async Task RunAsync_WithUnknownFlag_ReturnsFailure()
    {
        // Arrange — TuiArgumentParser rejects any "--" flag it does not recognize, before
        // any TUI application state is created.
        string[] args = ["--unknown-option"];

        // Act
        var exitCode = await TuiDispatcher.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
    }

    [Fact]
    public async Task RunAsync_WithMissingInputFile_ReturnsFailure()
    {
        // Arrange — a well-formed but nonexistent --file path fails the file-existence check
        // before any TUI application state is created.
        string[] args = ["--file", "/path/that/does/not/exist.csv"];

        // Act
        var exitCode = await TuiDispatcher.RunAsync(args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Failure);
    }
}
