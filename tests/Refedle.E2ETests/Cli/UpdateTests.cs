using AwesomeAssertions;
using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli;

public sealed class UpdateTests
{
    [Fact]
    public async Task Run_WithUpdateOnDevelopmentBuild_ExitsZeroAndPointsToInstallScript()
    {
        // Arrange — the test build carries the default 0.0.0-dev version, so 'update' must
        // refuse to self-update and direct the user to install.sh instead.
        string[] arguments = ["update"];

        // Act
        var result = await CliProcess.RunWithArgumentsAsync(arguments);

        // Assert
        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().Contain("development build");
        result.StandardOutput.Should().Contain("install.sh");
    }
}
