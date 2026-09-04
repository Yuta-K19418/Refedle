using System.Runtime.InteropServices;
using AwesomeAssertions;
using Refedle.App.Cli.Update;

namespace Refedle.Tests.App.Cli.Update;

public sealed class RidMapperTests
{
    public static TheoryData<string, Architecture, string> SupportedCombinations => new()
    {
        { "OSX", Architecture.Arm64, "osx-arm64" },
        { "LINUX", Architecture.X64, "linux-x64" },
        { "LINUX", Architecture.Arm64, "linux-arm64" },
    };

    public static TheoryData<string, Architecture, string> UnsupportedCombinations => new()
    {
        { "OSX", Architecture.X64, "macOS on Intel (osx-x64) is not supported" },
        { "WINDOWS", Architecture.X64, "Windows is not supported" },
        { "LINUX", Architecture.X86, "Unsupported platform/architecture" },
        { "FreeBSD", Architecture.Arm64, "Unsupported platform/architecture" },
    };

    [Theory]
    [MemberData(nameof(SupportedCombinations))]
    public void Resolve_WithSupportedCombination_ReturnsRid(string platformName, Architecture architecture, string expectedRid)
    {
        // Arrange
        var platform = OSPlatform.Create(platformName);

        // Act
        var result = RidMapper.Resolve(platform, architecture);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedRid);
    }

    [Theory]
    [MemberData(nameof(UnsupportedCombinations))]
    public void Resolve_WithUnsupportedCombination_FailsWithExplanation(string platformName, Architecture architecture, string expectedMessageFragment)
    {
        // Arrange
        var platform = OSPlatform.Create(platformName);

        // Act
        var result = RidMapper.Resolve(platform, architecture);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(expectedMessageFragment);
    }
}
