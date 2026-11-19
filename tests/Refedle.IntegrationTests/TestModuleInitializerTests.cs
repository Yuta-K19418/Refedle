using AwesomeAssertions;
using Spectre.Console;

namespace Refedle.IntegrationTests;

/// <summary>
/// Regression guard for <see cref="TestModuleInitializer"/>: if an initializer is removed,
/// renamed, or its value changed, the suite stays green on non-interactive hosts while the
/// guarded failure returns (the Windows CI real-console hang, or production error/status
/// writes leaking into the test-run console). These tests only read process-wide state, so
/// they cannot disturb other tests sharing the process.
/// </summary>
public sealed class TestModuleInitializerTests
{
    [Fact]
    public void DisableRealDriverIO_BeforeAnyApplicationCreation_SetsProcessEnvironmentVariableToOne()
    {
        // Arrange
        const string variableName = "DisableRealDriverIO";

        // Act
        string? value = Environment.GetEnvironmentVariable(variableName);

        // Assert
        value.Should().Be("1");
    }

    [Fact]
    public void SuppressConsoleNoise_BeforeAnyTest_RedirectsConsoleErrorToNullWriter()
    {
        // Arrange — the module initializer ran exactly once, before the first test.

        // Act
        TextWriter errorWriter = Console.Error;

        // Assert
        errorWriter.Should().BeSameAs(TextWriter.Null);
    }

    [Fact]
    public void SuppressConsoleNoise_BeforeAnyTest_RedirectsAnsiConsoleOutputToNullWriter()
    {
        // Arrange — the module initializer ran exactly once, before the first test.

        // Act
        TextWriter outWriter = AnsiConsole.Console.Profile.Out.Writer;

        // Assert
        outWriter.Should().BeSameAs(TextWriter.Null);
    }
}
