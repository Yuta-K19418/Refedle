using AwesomeAssertions;

namespace Refedle.Tests;

/// <summary>
/// Regression guard for <see cref="TestModuleInitializer"/>: if the initializer is removed,
/// renamed, or its value changed, the suite stays green on non-interactive hosts while the
/// Windows CI real-console hang returns. This test reads the process environment without
/// mutating it, so it cannot disturb other tests sharing the process.
/// </summary>
public sealed class TestModuleInitializerTests
{
    [Fact]
    public void DisableRealDriverIO_BeforeAnyApplicationCreation_SetsProcessEnvironmentVariableToOne()
    {
        // Arrange
        const string variableName = "DisableRealDriverIO";

        // Act
        var value = Environment.GetEnvironmentVariable(variableName);

        // Assert
        value.Should().Be("1");
    }
}
