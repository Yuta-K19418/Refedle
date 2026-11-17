using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed class VirtualTableSourceTests : IDisposable
{
    private readonly string _testFilePath;

    public VirtualTableSourceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"virtualTableSource_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Dispose_DisposesCacheAndReader()
    {
        // Arrange
        string csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = Refedle.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = Refedle.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = Refedle.Engine.Types.DataFormat.Csv
        };
        using VirtualTableSource source = new(indexer, schema, static _ => { });
        _ = source[0, 0]; // Ensure cache and reader are initialized

        // Act
        source.Dispose();

        // Assert
        // Verify indexer throws ObjectDisposedException
        Func<object> act = () => _ = source[0, 0];
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Indexer_WhenWindowHasMismatchedRow_ReportsReadFailureOnlyOnceAndReturnsEmptyString()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3\nval4,val5");
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = Refedle.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = Refedle.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = Refedle.Engine.Types.DataFormat.Csv
        };
        List<string> errors = [];
        using VirtualTableSource source = new(indexer, schema, errors.Add);

        // Act
        object first = source[0, 0];
        object second = source[1, 1];

        // Assert
        first.Should().Be(string.Empty);
        second.Should().Be(string.Empty);
        errors.Should().ContainSingle().Which.Should().Contain("(2)");
    }

    [Fact]
    public void Indexer_AfterReadFailure_ReturnsEmptyStringWithoutConsultingCache()
    {
        // Arrange - row 0 is cached by the first window; row 90 sits in a later window that fails
        IEnumerable<string> lines = Enumerable.Range(0, 100).Select(static i => i == 90 ? "bad" : $"val{i},x");
        File.WriteAllText(_testFilePath, "col1,col2\n" + string.Join('\n', lines));
        DataRowIndexer indexer = new(_testFilePath);
        indexer.BuildIndex();
        TableSchema schema = new()
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = Refedle.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = Refedle.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = Refedle.Engine.Types.DataFormat.Csv
        };
        List<string> errors = [];
        using VirtualTableSource source = new(indexer, schema, errors.Add);
        source[0, 0].Should().Be("val0");

        // Act
        object failed = source[95, 0];
        object cachedRowAfterFailure = source[0, 0];
        object otherRowAfterFailure = source[50, 1];

        // Assert
        failed.Should().Be(string.Empty);
        cachedRowAfterFailure.Should().Be(string.Empty);
        otherRowAfterFailure.Should().Be(string.Empty);
        errors.Should().ContainSingle();
    }
}
