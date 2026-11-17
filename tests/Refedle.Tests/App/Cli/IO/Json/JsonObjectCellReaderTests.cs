using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli.IO;
using Refedle.App.Cli.IO.Json;

namespace Refedle.Tests.App.Cli.IO.Json;

public sealed class JsonObjectCellReaderTests
{
    private static CellData ReadCell(string objectJson, string columnName, PooledValueBuffer valueBuffer) =>
        JsonObjectCellReader.ReadCell(
            Encoding.UTF8.GetBytes(objectJson), Encoding.UTF8.GetBytes(columnName), valueBuffer);

    [Fact]
    public void ReadCell_MultipleColumnsReadSequentially_KeepsEachValueValidWhenRead()
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();
        const string objectJson = """{"number":1.50,"text":"hello"}""";

        // Act — consume each cell's text immediately (the RecordProcessor pattern) so buffer
        // reuse across calls cannot corrupt a value before it is read; the first CellData is
        // held to also assert its signals, which outlive the buffer contents.
        CellData firstCell = ReadCell(objectJson, "number", valueBuffer);
        string first = firstCell.Value.ToString();
        string second = ReadCell(objectJson, "text", valueBuffer).Value.ToString();

        // Assert
        firstCell.Presence.Should().Be(CellPresence.Value);
        firstCell.Encoding.Should().Be(CellEncoding.Raw);
        first.Should().Be("1.50");
        second.Should().Be("hello");
    }

    [Fact]
    public void ReadCell_NonMatchingContainerValue_SkipsToLaterProperty()
    {
        // Arrange — a non-matching property whose value is a container exercises reader.Skip().
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"skip_me":{"nested":1},"target":2}""", "target", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.Raw);
        cell.Value.ToString().Should().Be("2");
    }

    [Fact]
    public void ReadCell_EscapedPropertyName_MatchesPlainUtf8Name()
    {
        // Arrange — the property name is \u-escaped in the source; ValueTextEquals must
        // still match it against the plain UTF-8 encoded column name.
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"\u0076alue":1}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Value.ToString().Should().Be("1");
    }

    [Fact]
    public void ReadCell_StringExceedingInitialBuffer_ReturnsFullValue()
    {
        // Arrange
        string longValue = new('x', 300); // > MinimumSize (256), forces buffer growth
        using PooledValueBuffer valueBuffer = new();

        // Establish the initial 256-char buffer with the short first value.
        _ = ReadCell("""{"value":"hi"}""", "value", valueBuffer).Value.Length;

        // Act
        CellData cell = ReadCell($$"""{"value":"{{longValue}}"}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.PlainText);
        cell.Value.ToString().Should().Be(longValue);
    }

    [Fact]
    public void ReadCell_StringWithEscapeSequences_ReturnsResolvedText()
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"value":"line1\n\"quoted\""}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.PlainText);
        cell.Value.ToString().Should().Be("line1\n\"quoted\"");
    }

    [Fact]
    public void ReadCell_EmptyString_ReturnsEmptyValueWithoutError()
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act — an empty string exercises Reserve's Math.Max(MinimumSize, 0) floor.
        CellData cell = ReadCell("""{"value":""}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.PlainText);
        cell.Value.ToString().Should().Be(string.Empty);
    }

    [Fact]
    public void ReadCell_AfterBufferDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        PooledValueBuffer valueBuffer = new();
        valueBuffer.Dispose();

        // Act
        Action act = () => { _ = ReadCell("""{"value":1.50}""", "value", valueBuffer); };

        // Assert — Reserve's guard catches the disposed buffer before any write.
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ReadCell_AfterWarmUp_AllocatesZeroBytes()
    {
        // Arrange
        byte[] objectBytes = Encoding.UTF8.GetBytes("""{"value":1.50}""");
        byte[] columnNameUtf8 = Encoding.UTF8.GetBytes("value");
        using PooledValueBuffer valueBuffer = new();

        // Warm up so the one-time pooled-buffer Reserve happens before measurement.
        _ = JsonObjectCellReader.ReadCell(objectBytes, columnNameUtf8, valueBuffer).Value.Length;

        // Act — steady-state ReadCell reuses the buffer and must allocate nothing.
        long before = GC.GetAllocatedBytesForCurrentThread();
        CellData cell = JsonObjectCellReader.ReadCell(objectBytes, columnNameUtf8, valueBuffer);
        long after = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        (after - before).Should().Be(0);
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Value.Length.Should().Be("1.50".Length);
    }

    [Fact]
    public void ReadCell_StringWithUtf8Characters_ReturnsFullText()
    {
        // Arrange
        string value = "日本語😀"; // CJK characters plus a surrogate-pair emoji
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell($$"""{"value":"{{value}}"}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.PlainText);
        cell.Value.ToString().Should().Be(value);
    }

    [Fact]
    public void ReadCell_EscapedUnicodeStringValue_ReturnsDecodedText()
    {
        // Arrange — \uXXXX escapes including a surrogate pair; CopyString must decode them.
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"value":"\u65e5\u672c\u8a9e\ud83d\ude00"}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.PlainText);
        cell.Value.ToString().Should().Be("日本語😀");
    }

    [Theory]
    [InlineData("""{"text":"日本語😀"}""")]
    [InlineData("""["日本語😀"]""")]
    public void ReadCell_RawStructuredValueWithUtf8_ReturnsFullRawText(string rawValue)
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell($$"""{"value":{{rawValue}}}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.Raw);
        cell.Value.ToString().Should().Be(rawValue);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void ReadCell_BooleanToken_ReturnsValueBoolean(string token)
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell($$"""{"value":{{token}}}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Value);
        cell.Encoding.Should().Be(CellEncoding.Boolean);
        cell.Value.ToString().Should().Be(token);
    }

    [Fact]
    public void ReadCell_NullToken_ReturnsNullPresence()
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"value":null}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Null);
    }

    [Fact]
    public void ReadCell_MissingProperty_ReturnsMissingPresence()
    {
        // Arrange
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell("""{"other":1}""", "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Missing);
    }

    [Theory]
    [InlineData("not valid json")]
    [InlineData("[1,2,3]")]
    public void ReadCell_MalformedOrNonObjectSource_ReturnsInvalidPresence(string source)
    {
        // Arrange — the second case is valid JSON but not an object.
        using PooledValueBuffer valueBuffer = new();

        // Act
        CellData cell = ReadCell(source, "value", valueBuffer);

        // Assert
        cell.Presence.Should().Be(CellPresence.Invalid);
    }
}
