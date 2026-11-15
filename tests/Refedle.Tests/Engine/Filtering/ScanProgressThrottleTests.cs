using AwesomeAssertions;
using Refedle.Engine.Filtering;

namespace Refedle.Tests.Engine.Filtering;

public sealed class ScanProgressThrottleTests
{
    [Fact]
    public void ShouldNotify_WhenAdvancedLessThanOnePercent_ReturnsFalse()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();

        // Act
        var result = throttle.ShouldNotify(scannedRows: 999, totalRows: 100_000);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(5_000)]
    public void ShouldNotify_WhenAdvancedAtLeastOnePercent_ReturnsTrue(int scannedRows)
    {
        // Arrange
        var throttle = new ScanProgressThrottle();

        // Act
        var result = throttle.ShouldNotify(scannedRows, totalRows: 100_000);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_MeasuresAdvanceFromLastNotifiedPosition()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();
        throttle.ShouldNotify(scannedRows: 1_000, totalRows: 100_000).Should().BeTrue();

        // Act
        var belowStep = throttle.ShouldNotify(scannedRows: 1_999, totalRows: 100_000);
        var atStep = throttle.ShouldNotify(scannedRows: 2_000, totalRows: 100_000);

        // Assert
        belowStep.Should().BeFalse();
        atStep.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_WhenFinalPositionIsBelowStep_StillReturnsTrue()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();
        throttle.ShouldNotify(scannedRows: 99_500, totalRows: 100_000).Should().BeTrue();

        // Act
        var result = throttle.ShouldNotify(scannedRows: 100_000, totalRows: 100_000);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_WhenTotalIsUnderOneHundredRows_NotifiesEveryPosition()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();

        // Act
        var results = Enumerable.Range(1, 3).Select(rows => throttle.ShouldNotify(rows, totalRows: 50)).ToArray();

        // Assert
        results.Should().OnlyContain(static notified => notified);
    }

    [Fact]
    public void ShouldNotify_WhenSinglePositionIsFinal_ReturnsTrue()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();

        // Act
        var result = throttle.ShouldNotify(scannedRows: 1, totalRows: 1);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_WhenLaterQualifyingPositionFollowsEarlierOne_NotifiesBoth()
    {
        // Arrange — 1000 and 3000 both qualify from 0; the retry path of a lost race is
        // equivalent to evaluating 3000 against the recorded 1000, which cannot be forced
        // deterministically with real threads
        var throttle = new ScanProgressThrottle();

        // Act
        var first = throttle.ShouldNotify(scannedRows: 1_000, totalRows: 100_000);
        var second = throttle.ShouldNotify(scannedRows: 3_000, totalRows: 100_000);

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_WhenOlderPositionArrivesLate_DoesNotMoveRecordedPositionBackwards()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();
        throttle.ShouldNotify(scannedRows: 3_000, totalRows: 100_000).Should().BeTrue();

        // Act
        var older = throttle.ShouldNotify(scannedRows: 1_000, totalRows: 100_000);
        var belowStepFromNewest = throttle.ShouldNotify(scannedRows: 3_999, totalRows: 100_000);

        // Assert
        older.Should().BeFalse();
        belowStepFromNewest.Should().BeFalse();
    }

    [Fact]
    public void ShouldNotify_WhenNonFinalPositionRepeatsWithZeroStep_ReturnsFalseSecondTime()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();
        throttle.ShouldNotify(scannedRows: 10, totalRows: 50).Should().BeTrue();

        // Act
        var repeated = throttle.ShouldNotify(scannedRows: 10, totalRows: 50);

        // Assert
        repeated.Should().BeFalse();
    }

    [Fact]
    public void ShouldNotify_WhenFinalPositionRepeats_ReturnsTrueEachTime()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();

        // Act
        var first = throttle.ShouldNotify(scannedRows: 50, totalRows: 50);
        var second = throttle.ShouldNotify(scannedRows: 50, totalRows: 50);

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void ShouldNotify_WhenOlderFinalPositionArrivesLate_DoesNotMoveRecordedPositionBackwards()
    {
        // Arrange — a final position records 150, then an older final position arrives late
        var throttle = new ScanProgressThrottle();
        throttle.ShouldNotify(scannedRows: 150, totalRows: 100).Should().BeTrue();

        // Act
        var olderFinal = throttle.ShouldNotify(scannedRows: 100, totalRows: 100);
        var nextScanOfLaterFile = throttle.ShouldNotify(scannedRows: 102, totalRows: 200);

        // Assert — the older final is still reported but must not roll the recorded position back
        olderFinal.Should().BeTrue();
        nextScanOfLaterFile.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotify_WhenCalledConcurrentlyForSamePosition_NotifiesOnce()
    {
        // Arrange
        var throttle = new ScanProgressThrottle();
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return throttle.ShouldNotify(scannedRows: 5_000, totalRows: 100_000);
            }))
            .ToArray();

        // Act
        start.Set();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(static notified => notified).Should().Be(1);
    }
}
