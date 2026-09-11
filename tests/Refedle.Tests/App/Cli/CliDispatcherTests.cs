using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class CliDispatcherTests : IDisposable
{
    private readonly string _testDir;

    public CliDispatcherTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithHelpCommand_ReturnsSuccess()
    {
        // Arrange

        // Act
        var exitCode = await CliDispatcher.RunAsync(CliCommand.Help, [], CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
    }

    [Fact]
    public async Task RunAsync_WithVersionCommand_ReturnsSuccess()
    {
        // Arrange

        // Act
        var exitCode = await CliDispatcher.RunAsync(CliCommand.Version, [], CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
    }

    [Fact]
    public async Task RunAsync_WithFullApplyInvocation_RunsApplyAndWritesOutput()
    {
        // Arrange — a full valid apply invocation only completes if the leading "apply" token
        // was removed before ApplyRunner parsed the arguments; otherwise argument parsing
        // rejects "apply" as an invalid flag and the exit code is Failure.
        var inputFile = CreateTestFile("input.csv", "name,age\nAlice,30");
        var recipeFile = CreateTestFile("recipe.yaml", "name: Empty\nactions: []");
        var outputFile = Path.Combine(_testDir, "output.csv");
        string[] args = ["apply", "--input", inputFile, "--recipe", recipeFile, "--output", outputFile];

        // Act
        var exitCode = await CliDispatcher.RunAsync(CliCommand.Apply, args, CancellationToken.None);

        // Assert
        exitCode.Should().Be(ExitCode.Success);
        File.Exists(outputFile).Should().BeTrue();
    }

    [Theory]
    [InlineData()]
    [InlineData("--input", "x.csv")]
    public async Task RunAsync_WithApplyCommand_WhenLeadingApplyTokenMissing_ThrowsArgumentException(
        params string[] args)
    {
        // Arrange — the matcher never yields CliCommand.Apply without a leading "apply" token;
        // reaching the dispatcher that way is a programming error, not a user-input condition.

        // Act
        var act = async () => await CliDispatcher.RunAsync(CliCommand.Apply, args, CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowExactlyAsync<ArgumentException>();
        thrown.Which.ParamName.Should().Be("args");
    }

    [Fact]
    public async Task RunAsync_WithUnknownCommand_ThrowsUnreachableException()
    {
        // Arrange — a value outside the declared CliCommand members, reachable only via a cast.
        var command = (CliCommand)int.MaxValue;

        // Act
        var act = async () => await CliDispatcher.RunAsync(command, [], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnreachableException>();
    }

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testDir, fileName);
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
    }
}
