using AwesomeAssertions;
using Refedle.App.Views;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Views;

public sealed class FocusedTableTransformerTests
{
    // -------------------------------------------------------------------------
    // Test double — mirrors FocusedTableSource's shape: column 0 is the "#" hash,
    // data columns start at index 1.
    // -------------------------------------------------------------------------

    private sealed class FakeFocusedSource(string[][] data, string[] columnNames) : ITableSource
    {
        public int Rows => data.Length;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public object this[int row, int col] => data[row][col];
    }

    private static TableSchema MakeSchema(params (string name, ColumnType type)[] cols) =>
        new TableSchema
        {
            Columns =
            [
                .. cols.Select(
                    (c, i) =>
                        new ColumnSchema
                        {
                            Name = c.name,
                            Type = c.type,
                            ColumnIndex = i,
                        }
                ),
            ],
            SourceFormat = DataFormat.JsonArray,
        };

    // -------------------------------------------------------------------------
    // Create — null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = MakeSchema(("name", ColumnType.Text));

        // Act
        var act = () => FocusedTableTransformer.Create(null!, schema, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOriginalSchema_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeFocusedSource([["[0]", "A"]], ["#", "name"]);

        // Act
        var act = () => FocusedTableTransformer.Create(source, null!, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeFocusedSource([["[0]", "A"]], ["#", "name"]);
        var schema = MakeSchema(("name", ColumnType.Text));

        // Act
        var act = () => FocusedTableTransformer.Create(source, schema, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // Schema transformation — "#" pseudo column
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithEmptyActions_PrependsHashColumnToLabeledSchema()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, []);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "name (text)", "val (number)");
        transformer.RawColumnNames.Should().Equal("#", "name", "val");
        transformer.Columns.Should().Be(3);
        transformer[0, 0].Should().Be("[0]");
    }

    [Fact]
    public void HashColumn_WhenActionsTargetLiteralHashName_IsSilentlySkipped()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "#", NewName = "hash" },
            new DeleteColumnAction { ColumnName = "#" },
            new FillColumnAction { ColumnName = "#", Value = "X" },
        ];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "name (text)", "val (number)");
        transformer.Columns.Should().Be(3);
        transformer[0, 0].Should().Be("[0]");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Rename
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithRenameAction_RenamesColumnAndKeepsValuesUnchanged()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions = [new RenameColumnAction { OldName = "name", NewName = "label" }];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "label (text)", "val (number)");
        transformer.RawColumnNames.Should().Equal("#", "label", "val");
        transformer[0, 1].Should().Be("A");
        transformer[1, 1].Should().Be("B");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Delete
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithDeleteAction_RemovesColumnAndResolvesRemainingColumnsAtOffset()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "name" }];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "val (number)");
        transformer.Columns.Should().Be(2);
        transformer[1, 0].Should().Be("[1]");
        transformer[1, 1].Should().Be("2");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Cast
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithCastAction_UpdatesTypeLabelAndFormatsCellValues()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1.50"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
            [new CastColumnAction { ColumnName = "val", TargetType = ColumnType.FloatingPoint }];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "name (text)", "val (float)");
        transformer[0, 2].Should().Be("1.5");
    }

    // -------------------------------------------------------------------------
    // Row resolution — Filter
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithFilterAction_RowsNarrowToMatchingSourceRows()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
                ["[2]", "C", "3"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
            [FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "B").Value];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("[1]");
        transformer[0, 1].Should().Be("B");
        transformer[0, 2].Should().Be("2");
    }

    [Fact]
    public void Filter_MultipleFilterActions_CombinesWithAndSemantics()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
                ["[2]", "B", "3"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "B").Value,
            FilterAction.Create("val", FilterOperator.GreaterThan, ComparisonType.Number, "2").Value,
        ];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("[2]");
        transformer[0, 1].Should().Be("B");
        transformer[0, 2].Should().Be("3");
    }

    [Fact]
    public void Filter_AfterRename_UsesCurrentActionStackColumnNames()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "name", NewName = "label" },
            FilterAction.Create("label", FilterOperator.Equals, ComparisonType.Text, "B").Value,
        ];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 1].Should().Be("B");
    }

    // -------------------------------------------------------------------------
    // Cell resolution — Fill
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithFillAction_AllCellsOfColumnReturnFillValue()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
                ["[1]", "B", "2"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions = [new FillColumnAction { ColumnName = "val", Value = "N/A" }];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "name (text)", "val (text)");
        transformer[0, 2].Should().Be("N/A");
        transformer[1, 2].Should().Be("N/A");
    }

    // -------------------------------------------------------------------------
    // Cell resolution — FormatTimestamp
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithFormatTimestampAction_ReformatsTimestampCells()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "2024-01-02T03:04:05"],
            ],
            ["#", "created_at"]
        );
        var schema = MakeSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<MorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using var transformer = FocusedTableTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames.Should().Equal("#", "created_at (datetime)");
        transformer[0, 0].Should().Be("[0]");
        transformer[0, 1].Should().Be("2024/01/02");
    }

    // -------------------------------------------------------------------------
    // Cell resolution — bounds and disposal guards
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void Indexer_OutOfBounds_ThrowsArgumentOutOfRangeException(int row, int col)
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        using var transformer = FocusedTableTransformer.Create(source, schema, []);

        // Act
        var act = () => _ = transformer[row, col];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var source = new FakeFocusedSource(
            [
                ["[0]", "A", "1"],
            ],
            ["#", "name", "val"]
        );
        var schema = MakeSchema(("name", ColumnType.Text), ("val", ColumnType.WholeNumber));
        var transformer = FocusedTableTransformer.Create(source, schema, []);
        transformer.Dispose();

        // Act
        var act = () => _ = transformer[0, 0];

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
