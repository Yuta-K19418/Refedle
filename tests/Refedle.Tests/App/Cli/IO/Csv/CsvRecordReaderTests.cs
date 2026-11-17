using AwesomeAssertions;
using nietras.SeparatedValues;
using Refedle.App.Cli.IO;
using Refedle.App.Cli.IO.Csv;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli.IO.Csv;

public sealed class CsvRecordReaderTests
{
    // CSV cells always carry Value presence; the encoding is heuristically classified.
    [Theory]
    [InlineData("007", CellEncoding.Numeric)]
    [InlineData("TRUE", CellEncoding.Boolean)]
    [InlineData("hello", CellEncoding.PlainText)]
    [InlineData("", CellEncoding.PlainText)]
    internal async Task GetCellData_CsvText_ReturnsValuePresenceAndClassifiedEncoding(string input, CellEncoding expectedEncoding)
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, $"value,filler\n{input},x\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            BatchOutputSchema outputSchema = new([new BatchOutputColumn("value", "value")], []);
            using CsvRecordReader recordReader = new(sepReader, outputSchema);
            await recordReader.MoveNextAsync(default);

            // Act
            CellData cell = recordReader.GetCellData(0);

            // Assert
            cell.Presence.Should().Be(CellPresence.Value);
            cell.Encoding.Should().Be(expectedEncoding);
            cell.Value.ToString().Should().Be(input);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // The CLI must keep rejecting rows whose column count differs from the header.
    [Fact]
    public async Task MoveNextAsync_WithMismatchedColumnCount_ThrowsInvalidDataException()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            BatchOutputSchema outputSchema = new([new BatchOutputColumn("value", "value")], []);
            using CsvRecordReader recordReader = new(sepReader, outputSchema);

            // Act - Sep enforces the header column count on the first data row
            Func<Task<bool>> act = () => recordReader.MoveNextAsync(default).AsTask();

            // Assert
            await act.Should().ThrowAsync<InvalidDataException>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value\nhello\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            BatchOutputSchema outputSchema = new([new BatchOutputColumn("value", "value")], []);
            CsvRecordReader recordReader = new(sepReader, outputSchema);
            recordReader.Dispose();

            // Act
            Action act = () => recordReader.ThrowIfDisposed();

            // Assert
            ObjectDisposedException exception = act.Should().Throw<ObjectDisposedException>().Which;
            exception.ObjectName.Should().Be(typeof(CsvRecordReader).FullName);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static BatchOutputSchema BuildOutputSchema(int filterColumnIndex, FilterOperator op, string value) =>
        new([new BatchOutputColumn("value", "value")], [new BatchFilterSpec(filterColumnIndex, ComparisonType.Text, op, value)]);

    [Fact]
    public async Task EvaluateFilters_FilterColumnBeyondCurrentRowColCount_ReturnsFalse()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            using CsvRecordReader recordReader = new(sepReader, BuildOutputSchema(5, FilterOperator.Equals, "hello"));
            await recordReader.MoveNextAsync(default);

            // Act
            bool result = recordReader.EvaluateFilters();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EvaluateFilters_MatchingStringFilter_ReturnsTrue()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            using CsvRecordReader recordReader = new(sepReader, BuildOutputSchema(0, FilterOperator.Equals, "hello"));
            await recordReader.MoveNextAsync(default);

            // Act
            bool result = recordReader.EvaluateFilters();

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task EvaluateFilters_NonMatchingStringFilter_ReturnsFalse()
    {
        // Arrange
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            SepReader sepReader = await CsvSep.FromFileAsync(filePath);
            using CsvRecordReader recordReader = new(sepReader, BuildOutputSchema(0, FilterOperator.Equals, "world"));
            await recordReader.MoveNextAsync(default);

            // Act
            bool result = recordReader.EvaluateFilters();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
