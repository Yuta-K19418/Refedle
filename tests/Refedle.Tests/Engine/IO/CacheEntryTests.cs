using AwesomeAssertions;
using Refedle.Engine.IO;

namespace Refedle.Tests.Engine.IO;

public sealed class CacheEntryTests
{
    [Fact]
    public void Clear_WhenCalled_SetsRowIndexToMinusOne()
    {
        // Arrange
        CacheEntry<int> entry = new() { RowIndex = 5, Value = 100 };
        int emptyValue = -1;

        // Act
        entry.Clear(emptyValue);

        // Assert
        entry.RowIndex.Should().Be(-1);
    }

    [Fact]
    public void Clear_WhenCalled_SetsValueToEmptyValue()
    {
        // Arrange
        CacheEntry<string> entry = new() { RowIndex = 5, Value = "test" };
        string emptyValue = string.Empty;

        // Act
        entry.Clear(emptyValue);

        // Assert
        entry.Value.Should().Be(string.Empty);
        entry.RowIndex.Should().Be(-1);
    }
}
