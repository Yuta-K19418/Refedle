using AwesomeAssertions;
using Refedle.App.Tui.Workers;
using Refedle.Engine.IO;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Types;
using Refedle.TestHelpers.IO;

namespace Refedle.Tests.App.Tui.Workers;

public sealed class RowIndexerFactoryTests : IDisposable
{
    private readonly TempFile _tempFile = TempFile.CreateEmpty(".tmp");

    private readonly string _testFilePath;

    public RowIndexerFactoryTests()
    {
        _testFilePath = _tempFile.Path;
    }

    public void Dispose() => _tempFile.Dispose();

    [Fact]
    public void Create_WithCsvFormat_ReturnsDataRowIndexer()
    {
        // Act
        IRowIndexer result = RowIndexerFactory.Create(DataFormat.Csv, _testFilePath);

        // Assert
        result.Should().BeOfType<DataRowIndexer>();
        result.FilePath.Should().Be(_testFilePath);
    }

    [Fact]
    public void Create_WithJsonLinesFormat_ReturnsRowIndexer()
    {
        // Act
        IRowIndexer result = RowIndexerFactory.Create(DataFormat.JsonLines, _testFilePath);

        // Assert
        result.Should().BeOfType<RowIndexer>();
        result.FilePath.Should().Be(_testFilePath);
    }

    [Fact]
    public void Create_WithUnsupportedFormat_ThrowsNotSupportedException()
    {
        // Act
        Func<IRowIndexer> act = () => RowIndexerFactory.Create((DataFormat)999, _testFilePath);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
