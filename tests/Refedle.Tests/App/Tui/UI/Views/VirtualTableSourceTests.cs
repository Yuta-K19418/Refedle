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
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        var indexer = new DataRowIndexer(_testFilePath);
        indexer.BuildIndex();
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = Refedle.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = Refedle.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = Refedle.Engine.Types.DataFormat.Csv
        };
        using var source = new VirtualTableSource(indexer, schema, static _ => { });
        _ = source[0, 0]; // Ensure cache and reader are initialized

        // Act
        source.Dispose();

        // Assert
        // Verify indexer throws ObjectDisposedException
        var act = () => _ = source[0, 0];
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Indexer_WhenWindowHasMismatchedRow_ReportsReadFailureOnlyOnce()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2\nval3\nval4,val5");
        var indexer = new DataRowIndexer(_testFilePath);
        indexer.BuildIndex();
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = Refedle.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = Refedle.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = Refedle.Engine.Types.DataFormat.Csv
        };
        List<string> errors = [];
        using var source = new VirtualTableSource(indexer, schema, errors.Add);

        // Act
        var first = source[0, 0];
        var second = source[1, 1];

        // Assert
        first.Should().Be(string.Empty);
        second.Should().Be(string.Empty);
        errors.Should().ContainSingle().Which.Should().Contain("(2)");
    }
}
