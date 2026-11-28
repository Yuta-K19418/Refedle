using AwesomeAssertions;
using Refedle.App.Cli.IO;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli.IO;

public sealed class RecordReaderAttributeTests
{
    [Theory]
    [InlineData(DataFormat.Csv)]
    [InlineData(DataFormat.JsonLines)]
    public void Constructor_WithFormat_ExposesFormat(DataFormat format)
    {
        // Arrange

        // Act
        RecordReaderAttribute attribute = new(format);

        // Assert
        attribute.Format.Should().Be(format);
    }
}
