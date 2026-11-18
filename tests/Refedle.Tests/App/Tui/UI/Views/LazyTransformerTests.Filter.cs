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
    // Filter — Equals
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_EqualsOperator_OnlyMatchingRowsReturned()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["Alice"],
                ["Bob"],
                ["Alice"],
            ],
            ["Name"]
        );
        TableSchema schema = MakeCsvSchema(("Name", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
        ];

        // Act — rows 0 and 2 match "Alice"
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.TotalRows.Should().Be(2);
        transformer[0, 0].Should().Be("Alice");
        transformer[1, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_ContainsOperator_SubstringMatchingRowsReturned()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["apple"],
                ["banana"],
                ["apricot"],
                ["cherry"],
            ],
            ["Fruit"]
        );
        TableSchema schema = MakeCsvSchema(("Fruit", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Fruit", FilterOperator.Contains, ComparisonType.Text, "ap").Value,
        ];

        // Act — rows 0 ("apple") and 2 ("apricot") contain "ap"
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.TotalRows.Should().Be(2);
        transformer[0, 0].Should().Be("apple");
        transformer[1, 0].Should().Be("apricot");
    }

    // -------------------------------------------------------------------------
    // Filter — AND semantics
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_MultipleFilterActions_AppliesAndSemantics()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["Alice", "30"],
                ["Bob", "25"],
                ["Alice", "20"],
                ["Charlie", "30"],
            ],
            ["Name", "Age"]
        );
        TableSchema schema = MakeCsvSchema(("Name", ColumnType.Text), ("Age", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
            FilterAction.Create("Age", FilterOperator.Equals, ComparisonType.Text, "30").Value,
        ];

        // Act — only row 0 ("Alice", "30") matches both filters
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.TotalRows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
        transformer[0, 1].Should().Be("30");
    }

    [Fact]
    public void Filter_NoMatchingRows_RowsIsZero()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        TableSchema schema = MakeCsvSchema(("Name", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Charlie").Value,
        ];

        // Act — no rows match "Charlie"
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert
        transformer.TotalRows.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Filter — column resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_TargetingRenamedColumn_CorrectlyResolved()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        TableSchema schema = MakeCsvSchema(("Name", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "Name", NewName = "FullName" },
            FilterAction.Create("FullName", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
        ];

        // Act — only row 0 ("Alice") matches
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.TotalRows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_TargetingDeletedColumn_SilentlySkipped()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["Alice", "active"],
                ["Bob", "inactive"],
            ],
            ["Name", "Status"]
        );
        TableSchema schema = MakeCsvSchema(("Name", ColumnType.Text), ("Status", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "Status" },
            // Filter targets a deleted column — should be silently skipped, no rows excluded
            FilterAction.Create("Status", FilterOperator.Equals, ComparisonType.Text, "active").Value,
        ];

        // Act — the filter spec is skipped (Status column was deleted), so no FilterSpec
        // is generated and the factory is never called; all source rows are exposed
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — both rows retained because the filter was silently skipped
        transformer.TotalRows.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Filter — numeric operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_GreaterThanOnWholeNumberColumn_ReturnsMatchingRows()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["10"],
                ["50"],
                ["30"],
                ["5"],
            ],
            ["Score"]
        );
        TableSchema schema = MakeCsvSchema(("Score", ColumnType.WholeNumber));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Score", FilterOperator.GreaterThan, ComparisonType.Number, "20").Value,
        ];

        // Act — rows 1 (50) and 2 (30) are greater than 20
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, [1, 2]);

        // Assert
        transformer.TotalRows.Should().Be(2);
        transformer[0, 0].Should().Be("50");
        transformer[1, 0].Should().Be("30");
    }

    [Fact]
    public void Filter_NumericOperatorOnTextColumn_ExcludesAllRows()
    {
        // Arrange — GreaterThan on a Text column always returns false, excluding all rows
        StringRowsFakeTableSource source = new(
            [
                ["hello"],
                ["world"],
            ],
            ["Word"]
        );
        TableSchema schema = MakeCsvSchema(("Word", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            FilterAction.Create("Word", FilterOperator.GreaterThan, ComparisonType.Number, "20").Value,
        ];

        // Act — numeric operators on Text columns always return false, so no rows match
        using LazyTransformer transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — all rows excluded because numeric operators are unsupported on Text columns
        transformer.TotalRows.Should().Be(0);
    }
}
