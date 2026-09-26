using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class RowIndexerFactoryTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerFactoryTests()
    {
        _testFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Create_WithCsvFormat_ReturnsDataRowIndexer()
    {
        // Act
        var result = RowIndexerFactory.Create(DataFormat.Csv, _testFilePath);

        // Assert
        result.Should().BeOfType<DataRowIndexer>();
        result.FilePath.Should().Be(_testFilePath);
    }

    [Fact]
    public void Create_WithJsonLinesFormat_ReturnsRowIndexer()
    {
        // Act
        var result = RowIndexerFactory.Create(DataFormat.JsonLines, _testFilePath);

        // Assert
        result.Should().BeOfType<RowIndexer>();
        result.FilePath.Should().Be(_testFilePath);
    }

    [Fact]
    public void Create_WithUnsupportedFormat_ThrowsNotSupportedException()
    {
        // Act
        var act = () => RowIndexerFactory.Create((DataFormat)999, _testFilePath);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
