using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using static Refedle.Tests.App.Tui.UI.TestSchemas;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class LazyTransformerTests
{
    // -------------------------------------------------------------------------
    // Cell value — formatting scope (only columns hit by an action are formatted)
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatTimestampAction_OnOneTimestampColumn_LeavesOtherTimestampColumnsRaw()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15T09:30:00", "2024-03-20T14:05:33"],
            ],
            ["created_at", "updated_at"]
        );
        TableSchema schema = MakeCsvSchema(
            ("created_at", ColumnType.Timestamp),
            ("updated_at", ColumnType.Timestamp)
        );
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object targeted = transformer[0, 0];
        object untouched = transformer[0, 1];

        // Assert
        targeted.Should().Be("2024/01/15");
        untouched.Should().Be("2024-03-20T14:05:33");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — FormatTimestamp
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatTimestampAction_OnTimestampColumn_FormatsValueWithTargetFormat()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
        );
        TableSchema schema = MakeCsvSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("2024/01/15");
    }

    [Fact]
    public void FormatTimestampAction_OnNonExistentColumn_IsSkipped()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "DoesNotExist",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("hello");
    }

    [Fact]
    public void FormatTimestampAction_AfterCastToTimestamp_FormatsCorrectly()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15"],
            ],
            ["created_at"]
        );
        TableSchema schema = MakeCsvSchema(("created_at", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "created_at", TargetType = ColumnType.Timestamp },
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("2024/01/15");
    }

    [Fact]
    public void MultipleFormatTimestampActions_LastOneWins()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
        );
        TableSchema schema = MakeCsvSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "MM/dd/yyyy",
            },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("01/15/2024");
    }

    [Fact]
    public void FormatTimestampAction_WithInvalidTimestampValue_ReturnsInvalidMarker()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["not-a-date"],
            ],
            ["created_at"]
        );
        TableSchema schema = MakeCsvSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("<invalid>");
    }

    [Fact]
    public void FormatTimestampAction_WithEmptyTargetFormat_UsesDefaultFormat()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
        );
        TableSchema schema = MakeCsvSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<IMorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = string.Empty,
            },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("2024-01-15 09:30:00");
    }

    [Fact]
    public void FormatTimestampAction_OverJsonLinesSource_LeavesOtherTimestampColumnsRaw()
    {
        // Arrange
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(
                path,
                ["{\"created_at\":\"2024-01-15T09:30:00\",\"updated_at\":\"2024-03-20T14:05:33\"}"]
            );
            RowIndexer indexer = new(path);
            indexer.BuildIndex();
            using RowByteCache cache = new(indexer);
            TableSchema schema = MakeCsvSchema(
                ("created_at", ColumnType.Timestamp),
                ("updated_at", ColumnType.Timestamp)
            );
            // The transformer disposes its wrapped source; the using guards the
            // transformer-construction gap (double dispose is a guarded no-op).
            using JsonLinesTableSource source = new(cache, schema);
            IReadOnlyList<IMorphAction> actions =
            [
                new FormatTimestampAction
                {
                    ColumnName = "created_at",
                    TargetFormat = "yyyy/MM/dd",
                },
            ];
            using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

            // Act
            object targeted = transformer[0, 0];
            object untouched = transformer[0, 1];

            // Assert
            targeted.Should().Be("2024/01/15");
            untouched.Should().Be("2024-03-20T14:05:33");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
