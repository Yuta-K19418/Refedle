using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Apply;
using Refedle.App.Cli.IO;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli.Commands.Apply;

// Cell-issue collection coverage: which cells are reported, with which row number, and how
// the detail list is capped at CellIssue.MaxReportedIssues entries.
public sealed partial class RecordProcessorTests
{
    private static readonly IReadOnlyList<BatchOutputColumn> _dateColumnWithFormat =
    [
        new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
    ];

    // -------------------------------------------------------------------------
    // Cell issues — timestamp parse failures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_UnparseableCell_ReportsCellIssue()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        TestRecordReader reader = new([["not-a-date"]], []);
        TestRecordWriter writer = new(null, (record) => writtenRecords.Add([.. record]));

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        result.HasMoreCellIssues.Should().BeFalse();
        CellIssue issue = result.CellIssues.Should().ContainSingle().Subject;
        issue.RowNumber.Should().Be(1);
        issue.ColumnName.Should().Be("col0");
        issue.RawValue.Should().Be("not-a-date");
        issue.Reason.Should().NotBeEmpty();
        writtenRecords.Should().ContainSingle().Which.Should().BeEquivalentTo(["not-a-date"]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_ReportedRowNumberReflectsInputPosition()
    {
        // Arrange — the unparseable cell sits on the 2nd data row; numbering is 1-based over
        // input data rows, so it must be reported as row 2 even though row 1 formats fine.
        TestRecordReader reader = new(
            [
                ["2024-03-15T10:30:00"],
                ["not-a-date"],
                ["2023-12-25T00:00:00"],
            ],
            []);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().ContainSingle().Which.RowNumber.Should().Be(2);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_ParseableCells_ReportsNoCellIssues()
    {
        // Arrange
        TestRecordReader reader = new(
            [
                ["2024-03-15T10:30:00"],
                ["2023-12-25T00:00:00"],
            ],
            []);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().BeEmpty();
        result.HasMoreCellIssues.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Cell issues — Invalid presence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithInvalidCell_OnTransformedColumn_ReportsIssueAndWritesCellUnchanged()
    {
        // Arrange — real readers hand Invalid cells an empty value (the source was unreadable).
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];
        TestRecordReader reader = new([[""]], [], presences: [[CellPresence.Invalid]]);
        TestRecordWriter writer = new(null, (record) => writtenRecords.Add([.. record]));

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        CellIssue issue = result.CellIssues.Should().ContainSingle().Subject;
        issue.RowNumber.Should().Be(1);
        issue.ColumnName.Should().Be("col0");
        issue.RawValue.Should().BeEmpty();
        issue.Reason.Should().NotBeEmpty();
        writtenRecords.Should().ContainSingle().Which.Should().BeEquivalentTo([""]);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidCell_OnNonTransformedColumn_ReportsIssue()
    {
        // Arrange — Invalid is a reader-layer signal, reported independently of any transform.
        List<string[]> writtenRecords = [];
        TestRecordReader reader = new(
            [["Alice", ""]],
            [],
            presences: [[CellPresence.Value, CellPresence.Invalid]]);
        TestRecordWriter writer = new(null, (record) => writtenRecords.Add([.. record]));

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _twoColumns, default);

        // Assert
        CellIssue issue = result.CellIssues.Should().ContainSingle().Subject;
        issue.RowNumber.Should().Be(1);
        issue.ColumnName.Should().Be("col1");
        issue.Reason.Should().NotBeEmpty();
        writtenRecords.Should().ContainSingle().Which.Should().BeEquivalentTo(["Alice", ""]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_OnInvalidCell_StillFillsAndReportsNoIssue()
    {
        // Arrange — FillSpec's deterministic overwrite applies to unreadable cells like any
        // other, and Fill has no reportable case, so no cell issue is collected.
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("ANON")),
        ];
        TestRecordReader reader = new([[""]], [], presences: [[CellPresence.Invalid]]);
        TestRecordWriter writer = new(null, (record) => writtenRecords.Add([.. record]));

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.CellIssues.Should().BeEmpty();
        result.HasMoreCellIssues.Should().BeFalse();
        writtenRecords.Should().ContainSingle().Which.Should().BeEquivalentTo(["ANON"]);
    }

    [Fact]
    public async Task ProcessAsync_WithNullCell_OnTransformedColumn_ReportsNoIssue()
    {
        // Arrange — Null/Missing are legitimate absence, deferred from reporting.
        TestRecordReader reader = new([[""]], [], presences: [[CellPresence.Null]]);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().BeEmpty();
        result.HasMoreCellIssues.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_WithMissingCell_OnTransformedColumn_ReportsNoIssue()
    {
        // Arrange
        TestRecordReader reader = new([[""]], [], presences: [[CellPresence.Missing]]);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().BeEmpty();
        result.HasMoreCellIssues.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Cell issues — filtered rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithFilter_RowExcludedByFilter_IsNotReported()
    {
        // Arrange — only rows that are written contribute issues.
        List<FilterSpec> filters =
        [
            new(0, ColumnType.Text, FilterOperator.Contains, "2024"),
        ];
        TestRecordReader reader = new(
            [
                ["not-a-date"],
                ["2024-03-15T10:30:00"],
            ],
            filters);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithFilter_ReportedRowNumberCountsFilteredOutRows()
    {
        // Arrange — row 1 is filtered out, so the reported issue on row 2 must still say 2:
        // row numbers identify positions in the input file, not in the output.
        List<FilterSpec> filters =
        [
            new(0, ColumnType.Text, FilterOperator.Contains, "second"),
        ];
        TestRecordReader reader = new(
            [
                ["not-a-date"],
                ["second bad value"],
            ],
            filters);
        TestRecordWriter writer = new(null, (_) => { });

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert
        result.CellIssues.Should().ContainSingle().Which.RowNumber.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Cell issues — detail cap
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    [InlineData(150, true)]
    public async Task ProcessAsync_WhenIssueCountReachesTheCap_CapsDetailAndFlagsMore(int rowCount, bool expectedHasMore)
    {
        // Arrange — every row fails to parse, so the issue count equals the row count.
        List<string[]> writtenRecords = [];
        string[][] rows = [.. Enumerable.Repeat("not-a-date", rowCount).Select(value => new[] { value })];
        TestRecordReader reader = new(rows, []);
        TestRecordWriter writer = new(null, (record) => writtenRecords.Add([.. record]));

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _dateColumnWithFormat, default);

        // Assert — all rows are still written; only the reported detail is capped.
        result.CellIssues.Should().HaveCount(Math.Min(rowCount, CellIssue.MaxReportedIssues));
        result.HasMoreCellIssues.Should().Be(expectedHasMore);
        writtenRecords.Should().HaveCount(rowCount);
    }
}
