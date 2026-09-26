using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views.JsonRangeTreeNodes;

namespace Refedle.Tests.App.Tui.Ui.Views.JsonRangeTreeNodes;

public sealed class RangePartitionPolicyTests
{
    [Fact]
    public void GetNodeGroupSize_SmallFile_ReturnsRangeSize()
    {
        // Arrange — 50KB file, estimated ~500 rows, well below 1,000,000 threshold
        const long fileSize = 50_000;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert
        result.Should().Be(1_000);
    }

    [Fact]
    public void GetNodeGroupSize_MediumFile_ReturnsRangeSize()
    {
        // Arrange — 50MB file, estimated ~500,000 rows, still below 1,000,000 threshold
        const long fileSize = 50_000_000;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert
        result.Should().Be(1_000);
    }

    [Fact]
    public void GetNodeGroupSize_AtExactThreshold_ReturnsRangeSize()
    {
        // Arrange — 100MB file, estimated ~1,000,000 rows (exactly at threshold)
        const long fileSize = 100_000_000;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert
        result.Should().Be(1_000);
    }

    [Fact]
    public void GetNodeGroupSize_JustAboveThreshold_ReturnsSuperRangeSize()
    {
        // Arrange — 100MB + 200 bytes, estimated ~1,000,002 rows (just above threshold)
        const long fileSize = 100_000_200;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert — rangesCount=1001, superFactor=2, superRangeSize=2000
        result.Should().Be(2_000);
    }

    [Fact]
    public void GetNodeGroupSize_LargeFile_ReturnsSuperRangeSize()
    {
        // Arrange — 1GB file, estimated ~10,737,418 rows, above 1,000,000 threshold
        const long fileSize = 1_073_741_824;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert — rangesCount=10738, superFactor=11, superRangeSize=11000
        result.Should().Be(11_000);
    }

    [Fact]
    public void GetNodeGroupSize_10GBFile_ReturnsCorrectSuperRangeSize()
    {
        // Arrange — 10GB file, estimated ~107,374,182 rows
        const long fileSize = 10_737_418_240;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert — rangesCount=107375, superFactor=108, superRangeSize=108000
        result.Should().Be(108_000);
    }

    [Fact]
    public void GetNodeGroupSize_100GBFile_ReturnsCorrectSuperRangeSize()
    {
        // Arrange — 100GB file, estimated ~1,073,741,824 rows
        const long fileSize = 107_374_182_400;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert — rangesCount=1073742, superFactor=1074, superRangeSize=1074000
        result.Should().Be(1_074_000);
    }

    [Fact]
    public void GetNodeGroupSize_ZeroFileSize_ReturnsRangeSize()
    {
        // Arrange — 0-byte file
        const long fileSize = 0;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert
        result.Should().Be(1_000);
    }

    [Fact]
    public void GetNodeGroupSize_MinimumNonZeroFileSize_ReturnsRangeSize()
    {
        // Arrange — 1-byte file (minimum non-zero), estimated 0 rows, well below threshold
        const long fileSize = 1;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert
        result.Should().Be(1_000);
    }

    [Fact]
    public void GetNodeGroupSize_FileSizeAtExactRecordBoundary_ReturnsRangeSize()
    {
        // Arrange — BytesPerRecord(100) * RangeSize(1000) = 100,000 bytes
        // estimatedRows = 100,000 / 100 = 1,000, well below 1,000,000 threshold
        const long fileSize = 100_000;

        // Act
        var result = RangePartitionPolicy.GetNodeGroupSize(fileSize);

        // Assert — boundary exactly at estimatedRows=1,000
        result.Should().Be(1_000);
    }
}
