using AwesomeAssertions;
using Refedle.App;
using Refedle.App.Cli.Commands.Apply;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Cli.Commands.Apply;

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
        List<string[]> writtenRecords = [];
        bool writtenHeader = false;

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []
        );

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];

        List<FilterSpec> filters =
        [
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "25")
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "20", "TX"],
            ],
            filters
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleFilters_ExcludesNonMatchingRecords()
    {
        // Arrange
        List<string[]> writtenRecords = [];

        List<FilterSpec> filters =
        [
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "20"),
            new(0, ColumnType.Text, FilterOperator.Contains, "a")
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "22", "TX"],
            ],
            filters
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Charlie", "22", "TX"]);
    }

    [Fact]
    public async Task ProcessAsync_WithAllRecordsFiltered_WritesOnlyHeader()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        bool writtenHeader = false;

        List<FilterSpec> filters =
        [
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "100")
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            filters
        );

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY", "Extra1"],
                ["Bob", "25", "CA", "Extra2"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _twoColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "30"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithSingleColumn_WritesOnlyFirstColumn()
    {
        // Arrange
        List<string[]> writtenRecords = [];

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _oneColumn, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];
        bool writtenHeader = false;

        TestRecordReader reader = new(
            [],
            []
        );

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];
        bool writtenHeader = false;
        CancellationTokenSource cts = new();

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            [],
            cts,
            0
        );

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
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
        List<string[]> writtenRecords = [];
        CancellationTokenSource cts = new();

        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
                ["Charlie", "22", "TX"],
            ],
            [],
            cts,
            2
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(
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
        List<string[]> writtenRecords = [];

        TestRecordReader reader = new(
            [
                ["", "", ""],
                ["NonEmpty", "Value", "Here"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["", "", ""]);
        writtenRecords[1].Should().BeEquivalentTo(["NonEmpty", "Value", "Here"]);
    }

    [Fact]
    public async Task ProcessAsync_WithWhitespaceCells_WritesWhitespace()
    {
        // Arrange
        List<string[]> writtenRecords = [];

        TestRecordReader reader = new(
            [
                ["  spaces  ", "\ttabs\t", " mixed "],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, _threeColumns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("ANON")),
            new BatchOutputColumn("col1", "col1"),
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "30"],
                ["Bob", "25"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["ANON", "30"]);
        writtenRecords[1].Should().BeEquivalentTo(["ANON", "25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_MultiColumnRecipe_OverwritesOnlyTargetColumn()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0"),
            new BatchOutputColumn("col1", "col1", new FillSpec("***")),
            new BatchOutputColumn("col2", "col2"),
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "alice@example.com", "NY"],
                ["Bob", "bob@example.com", "CA"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["Alice", "***", "NY"]);
        writtenRecords[1].Should().BeEquivalentTo(["Bob", "***", "CA"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_FilteredRowsAreNotWritten()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        List<FilterSpec> filters =
        [
            new(1, ColumnType.WholeNumber, FilterOperator.GreaterThan, "25")
        ];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("ANON")),
            new BatchOutputColumn("col1", "col1"),
        ];

        TestRecordReader reader = new(
            [
                ["Alice", "30"],
                ["Bob", "20"],
            ],
            filters
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["ANON", "30"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_EmptyStringValue_WritesEmptyStrings()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("")),
            new BatchOutputColumn("col1", "col1"),
        ];

        TestRecordReader reader = new(
            [["Alice", "30"]],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["", "30"]);
    }

    [Fact]
    public async Task ProcessAsync_WithFillTransform_EmptyDataset_WritesOnlyHeader()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        bool writtenHeader = false;
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new FillSpec("FILL")),
        ];

        TestRecordReader reader = new([], []);

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        TestRecordReader reader = new(
            [
                ["2024-03-15T10:30:00"],
                ["2023-12-25T00:00:00"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(2);
        writtenRecords[0].Should().BeEquivalentTo(["2024/03/15"]);
        writtenRecords[1].Should().BeEquivalentTo(["2023/12/25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_DifferentSourceFormat_ReformatsCorrectly()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy-MM-dd")),
        ];

        TestRecordReader reader = new(
            [["03/15/2024"]],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo(["2024-03-15"]);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessAsync_WithTimestampFormatSpec_UnparseableCell_PassesRawValueThrough(string rawValue)
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        TestRecordReader reader = new(
            [[rawValue]],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(1);
        writtenRecords[0].Should().BeEquivalentTo([rawValue]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_MixedParseableAndUnparseableCells_WritesAllRows()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        TestRecordReader reader = new(
            [
                ["2024-03-15T10:30:00"],
                ["not-a-date"],
                ["2023-12-25T00:00:00"],
            ],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenRecords.Should().HaveCount(3);
        writtenRecords[0].Should().BeEquivalentTo(["2024/03/15"]);
        writtenRecords[1].Should().BeEquivalentTo(["not-a-date"]);
        writtenRecords[2].Should().BeEquivalentTo(["2023/12/25"]);
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_EmptyDataset_WritesOnlyHeader()
    {
        // Arrange
        bool writtenHeader = false;
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0", new TimestampFormatSpec("yyyy/MM/dd")),
        ];

        TestRecordReader reader = new([], []);

        TestRecordWriter writer = new(
            () => writtenHeader = true,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
        writtenHeader.Should().BeTrue();
        writtenRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithTimestampFormatSpec_MultiColumnRecipe_OtherColumnsPassThrough()
    {
        // Arrange
        List<string[]> writtenRecords = [];
        IReadOnlyList<BatchOutputColumn> columns =
        [
            new BatchOutputColumn("col0", "col0"),
            new BatchOutputColumn("col1", "col1", new TimestampFormatSpec("yyyy/MM/dd")),
            new BatchOutputColumn("col2", "col2"),
        ];

        TestRecordReader reader = new(
            [["Alice", "2024-03-15T10:30:00", "NY"]],
            []
        );

        TestRecordWriter writer = new(
            null,
            (record) => writtenRecords.Add([.. record])
        );

        // Act
        BatchRunResult result = await RecordProcessor.ProcessAsync(reader, writer, columns, default);

        // Assert
        result.ExitCode.Should().Be(ExitCode.Success);
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
        List<string> events = [];
        TestRecordReader reader = new(
            [
                ["Alice", "30", "NY"],
                ["Bob", "25", "CA"],
            ],
            []);
        TestRecordWriter writer = new(
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
        List<string> events = [];
        TestRecordReader reader = new([], []);
        TestRecordWriter writer = new(
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
