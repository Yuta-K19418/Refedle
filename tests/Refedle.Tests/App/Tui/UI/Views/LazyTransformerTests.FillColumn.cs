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
    // Schema transformation — Fill
    // -------------------------------------------------------------------------

    [Fact]
    public void FillColumnAction_SingleColumn_AllCellsReturnFillValue()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1"],
                ["a2"],
                ["a3"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("FILLED");
        transformer[1, 0].Should().Be("FILLED");
        transformer[2, 0].Should().Be("FILLED");
    }

    [Fact]
    public void FillColumnAction_NonExistentColumn_IsIgnored()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "DoesNotExist", Value = "FILLED" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("a1");
    }

    [Fact]
    public void FillColumnAction_EmptyString_AllCellsReturnEmpty()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1"],
                ["a2"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = string.Empty },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be(string.Empty);
        transformer[1, 0].Should().Be(string.Empty);
    }

    [Fact]
    public void FillColumnAction_WithRename_FillAppliedAfterRename()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1", "b1"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
            new FillColumnAction { ColumnName = "X", Value = "FILLED" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer[0, 0].Should().Be("FILLED");
        transformer[0, 1].Should().Be("b1");
    }

    [Fact]
    public void FillColumnAction_SameColumnFilledTwice_LastValueWins()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["original"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "first" },
            new FillColumnAction { ColumnName = "A", Value = "second" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("second");
    }

    [Fact]
    public void FillColumnAction_MultipleColumns_OnlyTargetColumnFilled()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1", "b1", "c1"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "B", Value = "FILLED" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("a1");
        transformer[0, 1].Should().Be("FILLED");
        transformer[0, 2].Should().Be("c1");
    }

    [Fact]
    public void FillColumnAction_FillBeforeRename_FillPreservedAfterRename()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer[0, 0].Should().Be("FILLED");
    }

    [Fact]
    public void FillColumnAction_WithCast_FillValueBypassesCastFormatting()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["42"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            new FillColumnAction { ColumnName = "A", Value = "hello" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        // FillValue takes precedence over cast formatting
        transformer[0, 0].Should().Be("hello");
    }

    [Fact]
    public void FillColumnAction_BeforeCast_FillValueBypassesCastFormatting()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["42"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "hello" },
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        // A cast after the fill must not lose the raw fill override
        transformer[0, 0].Should().Be("hello");
    }

    [Fact]
    public void FillColumnAction_AfterCastAndRename_FillValueOverridesAndNeighborStaysRaw()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["007", "1.50"],
            ],
            ["count", "price"]
        );
        TableSchema schema = MakeCsvSchema(("count", ColumnType.Text), ("price", ColumnType.FloatingPoint));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "count", TargetType = ColumnType.WholeNumber },
            new RenameColumnAction { OldName = "count", NewName = "renamed" },
            new FillColumnAction { ColumnName = "renamed", Value = "N/A" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("N/A");
        transformer[0, 1].Should().Be("1.50");
    }

    [Fact]
    public void FillColumnAction_ColumnNames_PreservesAllColumnNames()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a1", "b1", "c1"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "B", Value = "FILLED" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        // All three columns should be present after fill action
        transformer.ColumnNames.Should().HaveCount(3);
        transformer.ColumnNames[0].Should().Be("A (text)");
        transformer.ColumnNames[1].Should().Be("B (text)");
        transformer.ColumnNames[2].Should().Be("C (text)");
        transformer.Columns.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Fill — type inference
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("42", "number")]
    [InlineData("3.14", "float")]
    [InlineData("true", "bool")]
    [InlineData("hello", "text")]
    [InlineData("", "text")]
    public void FillColumnAction_InfersTypeFromValue_HeaderLabelUpdated(
        string fillValue,
        string expectedLabel
    )
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["original"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.WholeNumber));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = fillValue },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be($"A ({expectedLabel})");
        transformer.RawColumnNames[0].Should().Be("A");
    }

    [Fact]
    public void FillColumnAction_NumberColumnFilledWithText_TypeChangesToText()
    {
        // Arrange
        StringRowsFakeTableSource source = new([["100"]], ["Price"]);
        TableSchema schema = MakeCsvSchema(("Price", ColumnType.WholeNumber));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "Price", Value = "N/A" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("Price (text)");
        transformer[0, 0].Should().Be("N/A");
    }

    [Fact]
    public void FillColumnAction_TextColumnFilledWithNumber_TypeChangesToNumber()
    {
        // Arrange
        StringRowsFakeTableSource source = new([["hello"]], ["Value"]);
        TableSchema schema = MakeCsvSchema(("Value", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new FillColumnAction { ColumnName = "Value", Value = "42" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("Value (number)");
        transformer[0, 0].Should().Be("42");
    }
}
