using System.Text;
using AwesomeAssertions;
using Refedle.Engine.IO.JsonLines;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed class PropertyNameScannerTests
{
    [Fact]
    public void ScanPropertyNames_WithEmptyInput_LeavesAccumulatorUnchanged()
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];

        // Act
        PropertyNameScanner.ScanPropertyNames([], seen, order);

        // Assert
        seen.Should().BeEmpty();
        order.Should().BeEmpty();
    }

    [Fact]
    public void ScanPropertyNames_WithSingleLine_AppendsKeysInOrder()
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> lines = [Encoding.UTF8.GetBytes("""{"b":1,"a":2,"c":3}""")];

        // Act
        PropertyNameScanner.ScanPropertyNames(lines, seen, order);

        // Assert
        order.Should().Equal(["b", "a", "c"]);
        seen.Should().HaveCount(3);
    }

    [Fact]
    public void ScanPropertyNames_AcrossBatchesWithOverlappingKeys_KeepsUnionInFirstAppearanceOrder()
    {
        // Arrange — repeated calls sharing one accumulator, simulating batched reads.
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> firstBatch =
        [
            Encoding.UTF8.GetBytes("""{"a":1,"b":2}"""),
            Encoding.UTF8.GetBytes("""{"b":3,"c":4}"""),
        ];
        List<JsonRawBytes> secondBatch =
        [
            Encoding.UTF8.GetBytes("""{"c":5,"d":6}"""),
            Encoding.UTF8.GetBytes("""{"a":7}"""),
        ];

        // Act
        PropertyNameScanner.ScanPropertyNames(firstBatch, seen, order);
        PropertyNameScanner.ScanPropertyNames(secondBatch, seen, order);

        // Assert
        order.Should().Equal(["a", "b", "c", "d"]);
        seen.Should().HaveCount(4);
    }

    [Fact]
    public void ScanPropertyNames_WithKeyOnlyInLaterBatch_IncludesKey()
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> firstBatch = [Encoding.UTF8.GetBytes("""{"a":1}""")];
        List<JsonRawBytes> secondBatch = [Encoding.UTF8.GetBytes("""{"a":1,"late":2}""")];

        // Act
        PropertyNameScanner.ScanPropertyNames(firstBatch, seen, order);
        PropertyNameScanner.ScanPropertyNames(secondBatch, seen, order);

        // Assert
        order.Should().Equal(["a", "late"]);
    }

    [Fact]
    public void ScanPropertyNames_WithMalformedLine_SkipsLineWithoutAffectingOthers()
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> lines =
        [
            Encoding.UTF8.GetBytes("""{"a":1}"""),
            Encoding.UTF8.GetBytes("not valid json"),
            Encoding.UTF8.GetBytes("""{"c":3}"""),
        ];

        // Act
        PropertyNameScanner.ScanPropertyNames(lines, seen, order);

        // Assert
        order.Should().Equal(["a", "c"]);
        seen.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("")]
    public void ScanPropertyNames_WithNonObjectLine_SkipsLine(string line)
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> lines =
        [
            Encoding.UTF8.GetBytes("""{"a":1}"""),
            Encoding.UTF8.GetBytes(line),
            Encoding.UTF8.GetBytes("""{"c":3}"""),
        ];

        // Act
        PropertyNameScanner.ScanPropertyNames(lines, seen, order);

        // Assert
        order.Should().Equal(["a", "c"]);
        seen.Should().HaveCount(2);
    }

    [Fact]
    public void ScanPropertyNames_WithNestedObjectAndArrayValues_DoesNotIncludeNestedKeys()
    {
        // Arrange
        HashSet<string> seen = [];
        List<string> order = [];
        List<JsonRawBytes> lines = [Encoding.UTF8.GetBytes("""{"a":{"nested":1},"b":[1,2],"c":3}""")];

        // Act
        PropertyNameScanner.ScanPropertyNames(lines, seen, order);

        // Assert
        order.Should().Equal(["a", "b", "c"]);
        seen.Should().HaveCount(3);
    }
}
