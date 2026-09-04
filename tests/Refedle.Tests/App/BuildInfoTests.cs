using AwesomeAssertions;
using Refedle.App;

namespace Refedle.Tests.App;

public sealed class BuildInfoTests
{
    [Fact]
    public void Version_WhenGenerated_IsNotEmpty()
    {
        // Arrange

        // Act
        var version = BuildInfo.Version;

        // Assert
        version.Should().NotBeNullOrEmpty();
    }
}
