using AwesomeAssertions;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed class FilterRowIndexerTests : IDisposable
{
    private readonly string _testFilePath =
        Path.Combine(Path.GetTempPath(), $"csvFilterRowIndexer_{Guid.NewGuid()}.csv");

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WithConsistentColumnCounts_IndexesMatchingRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "name,age\nalice,1\nbob,2\nalice,3");
        var filterIndexer = CreateFilterIndexer();

        // Act
        await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        filterIndexer.TotalMatchedRows.Should().Be(2);
        filterIndexer.GetSourceRow(1).Should().Be(2);
    }

    [Theory]
    [InlineData("name,age\nalice,1\nbob\nalice,3")]
    [InlineData("name,age\nalice,1\nbob,2,x\nalice,3")]
    [InlineData("name,age\nalice\nbob")]
    public async Task BuildIndexAsync_WithMismatchedColumnCount_ThrowsInvalidDataException(string csvContent)
    {
        // Arrange
        File.WriteAllText(_testFilePath, csvContent);
        var filterIndexer = CreateFilterIndexer();

        // Act
        var act = async () => await filterIndexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    private FilterRowIndexer CreateFilterIndexer()
    {
        var rowIndexer = new DataRowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "alice");
        return new FilterRowIndexer(rowIndexer, sourceColumnCount: 2, [spec]);
    }
}
