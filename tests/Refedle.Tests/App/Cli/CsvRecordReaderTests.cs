using AwesomeAssertions;
using nietras.SeparatedValues;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli;

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
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, $"value,filler\n{input},x\n");
            var sepReader = await Sep.New(',').Reader().FromFileAsync(filePath);
            var outputSchema = new BatchOutputSchema([new BatchOutputColumn("value", "value")], []);
            using var recordReader = new CsvRecordReader(sepReader, outputSchema);
            await recordReader.MoveNextAsync(default);

            // Act
            var cell = recordReader.GetCellData(0);

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

    [Fact]
    public async Task ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value\nhello\n");
            var sepReader = await Sep.New(',').Reader().FromFileAsync(filePath);
            var outputSchema = new BatchOutputSchema([new BatchOutputColumn("value", "value")], []);
            var recordReader = new CsvRecordReader(sepReader, outputSchema);
            recordReader.Dispose();

            // Act
            Action act = () => recordReader.ThrowIfDisposed();

            // Assert
            var exception = act.Should().Throw<ObjectDisposedException>().Which;
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
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            var sepReader = await Sep.New(',').Reader().FromFileAsync(filePath);
            using var recordReader = new CsvRecordReader(sepReader, BuildOutputSchema(5, FilterOperator.Equals, "hello"));
            await recordReader.MoveNextAsync(default);

            // Act
            var result = recordReader.EvaluateFilters();

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
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            var sepReader = await Sep.New(',').Reader().FromFileAsync(filePath);
            using var recordReader = new CsvRecordReader(sepReader, BuildOutputSchema(0, FilterOperator.Equals, "hello"));
            await recordReader.MoveNextAsync(default);

            // Act
            var result = recordReader.EvaluateFilters();

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
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "value,filler\nhello,x\n");
            var sepReader = await Sep.New(',').Reader().FromFileAsync(filePath);
            using var recordReader = new CsvRecordReader(sepReader, BuildOutputSchema(0, FilterOperator.Equals, "world"));
            await recordReader.MoveNextAsync(default);

            // Act
            var result = recordReader.EvaluateFilters();

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
