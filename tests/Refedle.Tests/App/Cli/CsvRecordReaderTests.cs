using AwesomeAssertions;
using nietras.SeparatedValues;
using Refedle.App.Cli;
using Refedle.Engine;

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
}
