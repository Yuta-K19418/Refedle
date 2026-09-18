using AwesomeAssertions;
using Refedle.App.Cli.Commands.Apply;

namespace Refedle.Tests.App.Cli.Commands.Apply;

public sealed class CellIssueReporterTests
{
    // -------------------------------------------------------------------------
    // ReportAsync — no issues
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReportAsync_WithNoCellIssues_WritesNoWarnings()
    {
        // Arrange
        var logger = new TestAppLogger();

        // Act
        await CellIssueReporter.ReportAsync([], hasMoreCellIssues: false, logger);

        // Assert
        logger.Warnings.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // ReportAsync — entry rendering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReportAsync_WithCellIssueHavingRawValue_WritesHeaderAndEntryWithQuotedValue()
    {
        // Arrange
        var logger = new TestAppLogger();
        var issues = new[]
        {
            new CellIssue(3, "date", "not-a-date", "not recognized as a timestamp"),
        };

        // Act
        await CellIssueReporter.ReportAsync(issues, hasMoreCellIssues: false, logger);

        // Assert
        logger.Warnings.Should().Equal(
            "Some source cells could not be processed as specified:",
            "Row 3, column \"date\": not recognized as a timestamp (\"not-a-date\")");
    }

    [Fact]
    public async Task ReportAsync_WithCellIssueHavingEmptyRawValue_OmitsTheValueFromTheEntry()
    {
        // Arrange — unreadable source cells carry no text to show.
        var logger = new TestAppLogger();
        var issues = new[]
        {
            new CellIssue(4, "date", "", "unreadable source value"),
        };

        // Act
        await CellIssueReporter.ReportAsync(issues, hasMoreCellIssues: false, logger);

        // Assert
        logger.Warnings.Should().Equal(
            "Some source cells could not be processed as specified:",
            "Row 4, column \"date\": unreadable source value");
    }

    [Fact]
    public async Task ReportAsync_WithMultipleCellIssues_WritesOneEntryPerIssueInOrder()
    {
        // Arrange
        var logger = new TestAppLogger();
        var issues = new[]
        {
            new CellIssue(1, "a", "x", "reason 1"),
            new CellIssue(2, "b", "y", "reason 2"),
        };

        // Act
        await CellIssueReporter.ReportAsync(issues, hasMoreCellIssues: false, logger);

        // Assert
        logger.Warnings.Should().HaveCount(3);
        logger.Warnings[1].Should().Contain("Row 1");
        logger.Warnings[2].Should().Contain("Row 2");
    }

    // -------------------------------------------------------------------------
    // ReportAsync — overflow indicator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReportAsync_WithHasMoreCellIssues_ClosesWithMoreThanCapIndicator()
    {
        // Arrange
        var logger = new TestAppLogger();
        var issues = new[]
        {
            new CellIssue(1, "date", "not-a-date", "not recognized as a timestamp"),
        };

        // Act
        await CellIssueReporter.ReportAsync(issues, hasMoreCellIssues: true, logger);

        // Assert — no exact overflow count, only the "more than N" indicator.
        logger.Warnings.Should().HaveCount(3);
        logger.Warnings[^1].Should().Be(
            $"More than {CellIssue.MaxReportedIssues} source cells could not be processed; only the first {CellIssue.MaxReportedIssues} are listed.");
    }
}
