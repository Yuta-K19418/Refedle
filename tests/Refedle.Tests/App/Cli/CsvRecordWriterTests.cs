using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli;

public sealed class CsvRecordWriterTests
{
    private static readonly BatchOutputSchema _outputSchema = new(
        [new BatchOutputColumn("value", "value")],
        []);

    // CSV output is always plain text, so Encoding is ignored; only Presence matters.
    // CellPresence is internal, so each case is standalone.
    [Fact]
    public async Task WriteCellData_Value_WritesEscapedValue()
    {
        // Arrange
        const string value = "hello, world";

        // Act
        var output = await WriteSingleCellAndReadAsync(CellPresence.Value, value);

        // Assert
        output.Should().Be("\"hello, world\"\n");
    }

    [Fact]
    public async Task WriteCellData_Null_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Null;

        // Act
        var output = await WriteSingleCellAndReadAsync(presence);

        // Assert
        output.Should().Be("\n");
    }

    [Fact]
    public async Task WriteCellData_Missing_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Missing;

        // Act
        var output = await WriteSingleCellAndReadAsync(presence);

        // Assert
        output.Should().Be("\n");
    }

    [Fact]
    public async Task WriteCellData_Invalid_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Invalid;

        // Act
        var output = await WriteSingleCellAndReadAsync(presence);

        // Assert
        output.Should().Be("\n");
    }

    // CsvRecordWriter never reads Encoding for a Value cell — the CSV text is written as-is
    // regardless of how the reader classified it.
    [Theory]
    [InlineData("007", CellEncoding.Numeric)]
    [InlineData("TRUE", CellEncoding.Boolean)]
    [InlineData("[1]", CellEncoding.Raw)]
    internal async Task WriteCellData_ValueWithEncoding_PreservesCsvText(string value, CellEncoding encoding)
    {
        // Arrange

        // Act
        var output = await WriteSingleCellAndReadAsync(CellPresence.Value, value, encoding);

        // Assert
        output.Should().Be($"{value}\n");
    }

    [Fact]
    public async Task WriteFooterAsync_IsNoOp()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        using var writer = new CsvRecordWriter(streamWriter, _outputSchema);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("x", CellPresence.Value));
        await writer.WriteEndRecordAsync(default);

        // Act
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert — no closing frame is appended.
        var output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("x\n");
    }

    [Fact]
    public void ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        var writer = new CsvRecordWriter(streamWriter, _outputSchema);
        writer.Dispose();

        // Act
        Action act = () => writer.ThrowIfDisposed();

        // Assert
        var exception = act.Should().Throw<ObjectDisposedException>().Which;
        exception.ObjectName.Should().Be(typeof(CsvRecordWriter).FullName);
    }

    private static async Task<string> WriteSingleCellAndReadAsync(CellPresence presence, string value = "", CellEncoding encoding = CellEncoding.PlainText)
    {
        using var stream = new MemoryStream();
        using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        using var writer = new CsvRecordWriter(streamWriter, _outputSchema);

        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData(value, presence, encoding));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
