using AwesomeAssertions;
using Refedle.App;
using Refedle.Engine.IO.DrillDown;

namespace Refedle.Tests.App;

public sealed class KeyPathFormatterTests
{
    [Fact]
    public void Format_WithEmptyPath_ReturnsRoot()
    {
        // Arrange — an empty path represents the root location
        IReadOnlyList<KeyPathSegment> path = [];

        // Act
        string result = KeyPathFormatter.Format(path, collapseIndices: false);

        // Assert
        result.Should().Be("root");
    }

    [Fact]
    public void Format_WithKeyOnlyPath_ReturnsJoinedKeys()
    {
        // Arrange — object-property segments joined by the breadcrumb separator
        IReadOnlyList<KeyPathSegment> path =
        [
            new KeyPathSegment("k1", KeyPathSegmentKind.Key),
            new KeyPathSegment("k2", KeyPathSegmentKind.Key),
        ];

        // Act
        string result = KeyPathFormatter.Format(path, collapseIndices: false);

        // Assert
        result.Should().Be("k1 > k2");
    }

    [Fact]
    public void Format_WithIndexSegment_AndCollapseIndicesTrue_ReturnsStarMarker()
    {
        // Arrange — Full Aggregation DrillDown collapses every index to [*]
        IReadOnlyList<KeyPathSegment> path =
        [
            new KeyPathSegment("items", KeyPathSegmentKind.Key),
            new KeyPathSegment("[2]", KeyPathSegmentKind.Index),
        ];

        // Act
        string result = KeyPathFormatter.Format(path, collapseIndices: true);

        // Assert
        result.Should().Be("items[*]");
    }

    [Fact]
    public void Format_WithIndexSegment_AndCollapseIndicesFalse_ReturnsLiteralIndex()
    {
        // Arrange — tree navigation and Single DrillDown keep the concrete index
        IReadOnlyList<KeyPathSegment> path =
        [
            new KeyPathSegment("items", KeyPathSegmentKind.Key),
            new KeyPathSegment("[2]", KeyPathSegmentKind.Index),
        ];

        // Act
        string result = KeyPathFormatter.Format(path, collapseIndices: false);

        // Assert
        result.Should().Be("items[2]");
    }
}
