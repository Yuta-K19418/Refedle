using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using static Refedle.Tests.App.Tui.UI.TestSchemas;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class LazyTransformerTests
{
    // -------------------------------------------------------------------------
    // Cell value — passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_EmptyActionStack_ReturnsSameValueAsSource()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Cell value — cast formatting
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("007", ColumnType.WholeNumber, "7")]
    [InlineData("1.50", ColumnType.FloatingPoint, "1.5")]
    [InlineData("TRUE", ColumnType.Boolean, "true")]
    [InlineData("FALSE", ColumnType.Boolean, "false")]
    public void Indexer_CastWithValidInput_ReturnsFormattedValue(
        string rawValue,
        ColumnType targetType,
        string expectedValue
    )
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                [rawValue],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("not-a-number", ColumnType.WholeNumber)]
    [InlineData("1.2.3", ColumnType.FloatingPoint)]
    [InlineData("not-a-bool", ColumnType.Boolean)]
    [InlineData("not-a-date", ColumnType.Timestamp)]
    public void Indexer_CastWithInvalidInput_ReturnsInvalidPlaceholder(
        string rawValue,
        ColumnType targetType
    )
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                [rawValue],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("<invalid>");
    }

    [Fact]
    public void Indexer_CastToText_ReturnsRawValue()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["007"],
            ],
            ["count"]
        );
        TableSchema schema = MakeCsvSchema(("count", ColumnType.WholeNumber));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "count", TargetType = ColumnType.Text },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("007");
    }

    [Theory]
    [InlineData(ColumnType.JsonObject, "{\"a\":1}")]
    [InlineData(ColumnType.JsonArray, "[1,2]")]
    public void Indexer_CastToJsonValueTypes_ReturnsRawValue(ColumnType targetType, string rawValue)
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                [rawValue],
            ],
            ["value"]
        );
        TableSchema schema = MakeCsvSchema(("value", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "value", TargetType = targetType },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be(rawValue);
    }

    [Fact]
    public void Indexer_WithActionOnOneColumn_UntouchedNumberAndBoolColumnsStayRaw()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-01-15T09:30:00", "007", "1.50", "TRUE"],
            ],
            ["created_at", "count", "price", "active"]
        );
        TableSchema schema = MakeCsvSchema(
            ("created_at", ColumnType.Timestamp),
            ("count", ColumnType.WholeNumber),
            ("price", ColumnType.FloatingPoint),
            ("active", ColumnType.Boolean)
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
        object count = transformer[0, 1];
        object price = transformer[0, 2];
        object active = transformer[0, 3];

        // Assert
        count.Should().Be("007");
        price.Should().Be("1.50");
        active.Should().Be("TRUE");
    }

    [Fact]
    public void Indexer_CastThenRenameAndDelete_PreservesTargetColumnFormatting()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [["discard", "007", "1.50"]],
            ["drop", "count", "price"]
        );
        TableSchema schema = MakeCsvSchema(
            ("drop", ColumnType.Text),
            ("count", ColumnType.Text),
            ("price", ColumnType.FloatingPoint)
        );
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "count", TargetType = ColumnType.WholeNumber },
            new RenameColumnAction { OldName = "count", NewName = "renamed" },
            new DeleteColumnAction { ColumnName = "drop" },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object targeted = transformer[0, 0];
        object untouched = transformer[0, 1];

        // Assert
        targeted.Should().Be("7");
        untouched.Should().Be("1.50");
    }

    // -------------------------------------------------------------------------
    // Error handling — out-of-range indexer access
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_NegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        Func<object> act = () => _ = transformer[-1, 0];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_NegativeCol_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        Func<object> act = () => _ = transformer[0, -1];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_RowExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        Func<object> act = () => _ = transformer[1, 0]; // only row 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_ColExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        Func<object> act = () => _ = transformer[0, 1]; // only col 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
