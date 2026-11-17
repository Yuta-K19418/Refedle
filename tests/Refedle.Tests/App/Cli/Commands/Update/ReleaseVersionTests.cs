using AwesomeAssertions;
using Refedle.App.Cli.Commands.Update;

namespace Refedle.Tests.App.Cli.Commands.Update;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.3.0", 0, 3, 0)]
    [InlineData("v0.3.0", 0, 3, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v10.20.30", 10, 20, 30)]
    [InlineData("0.0.0", 0, 0, 0)]
    public void TryParse_WithValidVersion_ReturnsTrueAndComponents(string text, int major, int minor, int patch)
    {
        // Arrange

        // Act
        bool parsed = ReleaseVersion.TryParse(text, out ReleaseVersion version);

        // Assert
        parsed.Should().BeTrue();
        version.Should().Be(new ReleaseVersion(major, minor, patch));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("1..3")]
    [InlineData("0.0.0-dev")]
    [InlineData("-1.2.3")]
    [InlineData("1.-2.3")]
    [InlineData("v")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    public void TryParse_WithInvalidVersion_ReturnsFalseAndDefault(string text)
    {
        // Arrange

        // Act
        bool parsed = ReleaseVersion.TryParse(text, out ReleaseVersion version);

        // Assert
        parsed.Should().BeFalse();
        version.Should().Be(default(ReleaseVersion));
    }

    [Theory]
    [InlineData(0, 2, 0, 0, 3, 0)]
    [InlineData(0, 3, 0, 0, 3, 1)]
    [InlineData(0, 3, 0, 1, 0, 0)]
    [InlineData(0, 9, 9, 1, 0, 0)]
    [InlineData(1, 0, 0, 2, 0, 0)]
    public void ComparisonOperators_WhenLeftIsOlder_OrderLeftBeforeRight(
        int olderMajor, int olderMinor, int olderPatch, int newerMajor, int newerMinor, int newerPatch)
    {
        // Arrange
        ReleaseVersion older = new(olderMajor, olderMinor, olderPatch);
        ReleaseVersion newer = new(newerMajor, newerMinor, newerPatch);

        // Act

        // Assert
        (older < newer).Should().BeTrue();
        (older <= newer).Should().BeTrue();
        (newer > older).Should().BeTrue();
        (newer >= older).Should().BeTrue();
        (older >= newer).Should().BeFalse();
    }

    [Fact]
    public void ComparisonOperators_WhenVersionsAreEqual_TreatEitherOrderAsUpToDate()
    {
        // Arrange
        ReleaseVersion left = new(1, 2, 3);
        ReleaseVersion right = new(1, 2, 3);

        // Act

        // Assert
        (left >= right).Should().BeTrue();
        (left <= right).Should().BeTrue();
        (left < right).Should().BeFalse();
        (left > right).Should().BeFalse();
        left.Should().Be(right);
    }

    [Fact]
    public void ToString_Always_PrependsVPrefix()
    {
        // Arrange
        ReleaseVersion version = new(0, 3, 0);

        // Act
        string text = version.ToString();

        // Assert
        text.Should().Be("v0.3.0");
    }
}
