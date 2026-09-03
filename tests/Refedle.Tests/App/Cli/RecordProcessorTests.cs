using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli;

public sealed partial class RecordProcessorTests
{
    private static readonly IReadOnlyList<BatchOutputColumn> _oneColumn = [
        new BatchOutputColumn("col0", "col0"),
    ];

    private static readonly IReadOnlyList<BatchOutputColumn> _twoColumns = [
        new BatchOutputColumn("col0", "col0"),
        new BatchOutputColumn("col1", "col1"),
    ];

    private static readonly IReadOnlyList<BatchOutputColumn> _threeColumns = [
        new BatchOutputColumn("col0", "col0"),
        new BatchOutputColumn("col1", "col1"),
        new BatchOutputColumn("col2", "col2"),
    ];

    // -------------------------------------------------------------------------
    // ProcessAsync — Basic flow with valid data
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithValidData_WritesCorrectRecords()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var writtenHeader = false;

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "25", "CA"]);
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithFilter_ExcludesNonMatchingRecords()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var filters = new List<FilterSpec>
        {
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "25")
        };

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "20", "TX"],
            ],
            filters
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleFilters_ExcludesNonMatchingRecords()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var filters = new List<FilterSpec>
        {
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "20"),
            new(0, ColumnType.Text, FilterOperator.Contains, "a")
        };

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "22", "TX"],
            ],
            filters
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Charlie", "22", "TX"]);
    }

    [Fact]
    public async Task ProcessAsync_WithAllRecordsFiltered_WritesOnlyHeader()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var writtenHeader = false;

        var filters = new List<FilterSpec>
        {
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "100")
        };

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            filters
        );

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Column count
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithPartialColumns_WritesOnlySpecifiedColumns()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY", "Extra1"],
                ["Bob", "25", "CA", "Extra2"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _twoColumns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithSingleColumn_WritesOnlyFirstColumn()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _oneColumn, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob"]);
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Empty input
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithNoRecords_WritesOnlyHeader()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var writtenHeader = false;

        var reader = new TestRecordReader(
            [],
            []
        );

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Cancellation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithCancellation_BeforeWritingRecords_WritesOnlyHeader()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var writtenHeader = false;
        var cts = new CancellationTokenSource();

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            [],
            cts,
            0
        );

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, cts.Token));

        // Assert
        cts.Dispose();
        exception.Should().NotBeNull();
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithCancellation_AfterWritingSomeRecords_StopsProcessing()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var cts = new CancellationTokenSource();

        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "22", "TX"],
            ],
            [],
            cts,
            2
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, cts.Token));

        // Assert
        cts.Dispose();
        exception.Should().NotBeNull();
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "25", "CA"]);
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithEmptyCells_WritesEmptyStrings()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var reader = new TestRecordReader(
            [
                ["", "", ""],
                ["NonEmpty", "Value", "Here"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["", "", ""]);
        writtenRecords[1].Should().BeEquivalentTo(["NonEmpty", "Value", "Here"]);
    }

    [Fact]
    public async Task ProcessAsync_WithWhitespaceCells_WritesWhitespace()
    {
        // Arrange
        var writtenRecords = new List<string[]>();

        var reader = new TestRecordReader(
            [
                ["  spaces  ", "\ttabs\t", " mixed "],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["  spaces  ", "\ttabs\t", " mixed "]);
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — Fill transform
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithFillTransform_SingleColumn_OverwritesCellValues()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("ANON")),
            new BatchOutputColumn("col1", "col1"),
        ];

        var reader = new TestRecordReader(
            [
                ["Alice", "30"],
                ["Bob", "25"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["ANON", "30"]);
        writtenRecords[1].Should().BeEquivalentTo(["ANON", "25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_MultiColumnRecipe_OverwritesOnlyTargetColumn()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0"),
            new BatchOutputColumn("col1", "col1", new FillSpec("***")),
            new BatchOutputColumn("col2", "col2"),
        ];

        var reader = new TestRecordReader(
            [
                ["Alice", "alice@example.com", "NY"],
                ["Bob", "bob@example.com", "CA"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "***", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "***", "CA"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_FilteredRowsAreNotWritten()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var filters = new List<FilterSpec>
        {
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "25")
        };
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("ANON")),
            new BatchOutputColumn("col1", "col1"),
        ];

        var reader = new TestRecordReader(
            [
                ["Alice", "30"],
                ["Bob", "20"],
            ],
            filters
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["ANON", "30"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_EmptyStringValue_WritesEmptyStrings()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("")),
            new BatchOutputColumn("col1", "col1"),
        ];

        var reader = new TestRecordReader(
            [["Alice", "30"]],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["", "30"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_EmptyDataset_WritesOnlyHeader()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        var writtenHeader = false;
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("FILL")),
        ];

        var reader = new TestRecordReader([], []);

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — TimestampFormatSpec transform
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_ISO8601_ToCustomFormat_ReformatsCorrectly()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        var reader = new TestRecordReader(
            [
                ["2024-03-15T10:30:00"],
                ["2023-12-25T00:00:00"],
            ],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["2024/03/15"]);
        writtenRecords[1].Should().BeEquivalentTo(["2023/12/25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_DifferentSourceFormat_ReformatsCorrectly()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy-MM-dd")),
        ];

        var reader = new TestRecordReader(
            [["03/15/2024"]],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["2024-03-15"]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_UnparseableCell_ThrowsFormatException()
    {
        // Arrange
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        var reader = new TestRecordReader(
            [["not-a-date"]],
            []
        );

        var writer = new TestRecordWriter(null, null);

        // Act
        var act = async () => await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_EmptyDataset_WritesOnlyHeader()
    {
        // Arrange
        var writtenHeader = false;
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        var reader = new TestRecordReader([], []);

        var writer = new TestRecordWriter(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_MultiColumnRecipe_OtherColumnsPassThrough()
    {
        // Arrange
        var writtenRecords = new List<string[]>();
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0"),
            new BatchOutputColumn("col1", "col1", new TimestampFormatSpec("yyyy/MM/dd")),
            new BatchOutputColumn("col2", "col2"),
        ];

        var reader = new TestRecordReader(
            [["Alice", "2024-03-15T10:30:00", "NY"]],
            []
        );

        var writer = new TestRecordWriter(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        var result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.Should().Be(0);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "2024/03/15", "NY"]);
    }

    // -------------------------------------------------------------------------
    // ProcessAsync — WriteFooterAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CallsWriteFooterOnce_AfterTheRecordLoopAndBeforeFlush()
    {
        // Arrange
        var events = new List<string>();
        var reader = new TestRecordReader(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []);
        var writer = new TestRecordWriter(
            writeHeaderCallback: () => events.Add("header"),
            writeCellCallback: _ => events.Add("record"),
            writeFooterCallback: () => events.Add("footer"),
            flushCallback: () => events.Add("flush"));

        // Act
        await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        events.Should().Equal(["header", "record", "record", "footer", "flush"]);
    }

    [Fact]
    public async Task ProcessAsync_WithNoRecords_StillCallsWriteFooterBeforeFlush()
    {
        // Arrange
        var events = new List<string>();
        var reader = new TestRecordReader([], []);
        var writer = new TestRecordWriter(
            writeHeaderCallback: () => events.Add("header"),
            writeCellCallback: _ => events.Add("record"),
            writeFooterCallback: () => events.Add("footer"),
            flushCallback: () => events.Add("flush"));

        // Act
        await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        events.Should().Equal(["header", "footer", "flush"]);
    }
}
