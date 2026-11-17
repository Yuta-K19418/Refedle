using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli.IO;
using Refedle.App.Cli.IO.Csv;
using Refedle.Engine;

namespace Refedle.Tests.App.Cli.IO.Csv;

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
        string output = await WriteSingleCellAndReadAsync(CellPresence.Value, value);

        // Assert
        output.Should().Be("\"hello, world\"\n");
    }

    [Fact]
    public async Task WriteCellData_Null_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Null;

        // Act
        string output = await WriteSingleCellAndReadAsync(presence);

        // Assert
        output.Should().Be("\n");
    }

    [Fact]
    public async Task WriteCellData_Missing_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Missing;

        // Act
        string output = await WriteSingleCellAndReadAsync(presence);

        // Assert
        output.Should().Be("\n");
    }

    [Fact]
    public async Task WriteCellData_Invalid_WritesEmpty()
    {
        // Arrange
        const CellPresence presence = CellPresence.Invalid;

        // Act
        string output = await WriteSingleCellAndReadAsync(presence);

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
        string output = await WriteSingleCellAndReadAsync(CellPresence.Value, value, encoding);

        // Assert
        output.Should().Be($"{value}\n");
    }

    [Fact]
    public async Task WriteFooterAsync_IsNoOp()
    {
        // Arrange
        using MemoryStream stream = new();
        using StreamWriter streamWriter = new(stream, new UTF8Encoding(false), leaveOpen: true);
        using CsvRecordWriter writer = new(streamWriter, _outputSchema);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("x", CellPresence.Value));
        await writer.WriteEndRecordAsync(default);

        // Act
        await writer.WriteFooterAsync(default);
        await writer.FlushAsync(default);

        // Assert — no closing frame is appended.
        string output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("x\n");
    }

    [Fact]
    public async Task Constructor_WithCrlfConfiguredWriter_PinsLineEndingsToLf()
    {
        // Arrange — an explicitly CRLF-configured StreamWriter proves the constructor's NewLine
        // override deterministically, independent of the host OS's Environment.NewLine.
        using MemoryStream stream = new();
        using StreamWriter streamWriter = new(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n" };
        using CsvRecordWriter writer = new(streamWriter, _outputSchema);
        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData("x", CellPresence.Value));
        await writer.WriteEndRecordAsync(default);

        // Act
        await writer.FlushAsync(default);

        // Assert — the record terminator is LF regardless of the injected writer's NewLine.
        string output = Encoding.UTF8.GetString(stream.ToArray());
        output.Should().Be("x\n");
    }

    [Fact]
    public void ThrowIfDisposed_AfterDispose_ThrowsWithConcreteTypeName()
    {
        // Arrange
        using MemoryStream stream = new();
        using StreamWriter streamWriter = new(stream, new UTF8Encoding(false), leaveOpen: true);
        CsvRecordWriter writer = new(streamWriter, _outputSchema);
        writer.Dispose();

        // Act
        Action act = () => writer.ThrowIfDisposed();

        // Assert
        ObjectDisposedException exception = act.Should().Throw<ObjectDisposedException>().Which;
        exception.ObjectName.Should().Be(typeof(CsvRecordWriter).FullName);
    }

    private static async Task<string> WriteSingleCellAndReadAsync(CellPresence presence, string value = "", CellEncoding encoding = CellEncoding.PlainText)
    {
        using MemoryStream stream = new();
        using StreamWriter streamWriter = new(stream, new UTF8Encoding(false), leaveOpen: true);
        using CsvRecordWriter writer = new(streamWriter, _outputSchema);

        await writer.WriteStartRecordAsync(default);
        writer.WriteCellData(0, new CellData(value, presence, encoding));
        await writer.WriteEndRecordAsync(default);
        await writer.FlushAsync(default);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
