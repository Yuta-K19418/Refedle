using AwesomeAssertions;
using Refedle.App.Views;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Views;

public sealed class LazyTransformerTests
{
    // -------------------------------------------------------------------------
    // Test double
    // -------------------------------------------------------------------------

    private sealed class FakeTableSource(string[][] data, string[] columnNames) : ITableSource
    {
        public int Rows => data.Length;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public object this[int row, int col] => data[row][col];
    }

    /// <summary>
    /// A synchronous stub that returns pre-computed matched row indices without any filtering logic.
    /// </summary>
    private sealed class SyncFilterRowIndexer(IReadOnlyList<int> matchedRows) : IFilterRowIndexer
    {
        public int TotalMatchedRows => matchedRows.Count;

        public int GetSourceRow(int filteredIndex) => matchedRows[filteredIndex];

        public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class DisposableFakeTableSource(string[][] data, string[] columnNames)
        : ITableSource, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public int Rows => data.Length;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public object this[int row, int col] => data[row][col];
        public void Dispose() => IsDisposed = true;
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
            SourceFormat = DataFormat.Csv,
        };

    private static LazyTransformer MakeFilteredTransformer(
        FakeTableSource source,
        TableSchema schema,
        IReadOnlyList<MorphAction> actions,
        IReadOnlyList<int> matchedRows
    ) => new LazyTransformer(source, schema, actions, _ => new SyncFilterRowIndexer(matchedRows));

    // -------------------------------------------------------------------------
    // Constructor — null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = MakeSchema(("A", ColumnType.Text));

        // Act
        var act = () => new LazyTransformer(null!, schema, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOriginalSchema_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );

        // Act
        var act = () => new LazyTransformer(source, null!, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));

        // Act
        var act = () => new LazyTransformer(source, schema, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Rename
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithRenameAction_OutputSchemaReflectsNewName()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer.ColumnNames[1].Should().Be("B (text)");
    }

    [Fact]
    public void Constructor_WithRenameAction_SourceColumnIndexPreserved()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Delete
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithDeleteAction_DeletedColumnAbsentFromOutputSchema()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(2);
        transformer.ColumnNames.Should().BeEquivalentTo(["A (text)", "C (text)"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Constructor_WithDeleteAction_SourceColumnIndicesMappedCorrectly()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 1]; // output col 1 → source col 2 (C)

        // Assert
        result.Should().Be("c");
    }

    [Fact]
    public void Constructor_AllColumnsDeleted_ColumnsIsZero()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Cast
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithCastAction_OutputSchemaReflectsNewType()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["42"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        // ColumnType is reflected through FormatCellValue behaviour: valid integer is returned as-is
        transformer[0, 0].Should().Be("42");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Ordered actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithRenameFollowedByDelete_OperatesOnRenamedName()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
            new DeleteColumnAction { ColumnName = "X" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
        transformer.ColumnNames[0].Should().Be("B (text)");
    }

    // -------------------------------------------------------------------------
    // Cell value — passthrough
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_EmptyActionStack_ReturnsSameValueAsSource()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Cell value — cast formatting
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("42", ColumnType.WholeNumber, "42")]
    [InlineData("3.14", ColumnType.FloatingPoint, "3.14")]
    [InlineData("true", ColumnType.Boolean, "true")]
    public void Indexer_CastWithValidInput_ReturnsFormattedValue(
        string rawValue,
        ColumnType targetType,
        string expectedValue
    )
    {
        // Arrange
        var source = new FakeTableSource(
            [
                [rawValue],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("not-a-number", ColumnType.WholeNumber)]
    [InlineData("not-a-bool", ColumnType.Boolean)]
    [InlineData("not-a-date", ColumnType.Timestamp)]
    public void Indexer_CastWithInvalidInput_ReturnsInvalidPlaceholder(
        string rawValue,
        ColumnType targetType
    )
    {
        // Arrange
        var source = new FakeTableSource(
            [
                [rawValue],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = targetType },
        ];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("<invalid>");
    }

    // -------------------------------------------------------------------------
    // Error handling — silently skipped actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_ActionTargetingNonExistentColumn_IsSilentlySkipped()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "DoesNotExist" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // Error handling — out-of-range indexer access
    // -------------------------------------------------------------------------

    [Fact]
    public void Indexer_NegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[-1, 0];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_NegativeCol_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[0, -1];

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_RowExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[1, 0]; // only row 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_ColExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var act = () => _ = transformer[0, 1]; // only col 0 exists

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    [Fact]
    public void Rows_DelegatesToUnderlyingSource()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a"],
                ["b"],
                ["c"],
                ["d"],
                ["e"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        using var transformer = new LazyTransformer(source, schema, []);

        // Act
        var rows = transformer.Rows;

        // Assert
        rows.Should().Be(5);
    }

    [Fact]
    public void Columns_ReflectsTransformedSchemaColumnCount()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var columns = transformer.Columns;

        // Assert
        columns.Should().Be(2);
    }

    [Fact]
    public void ColumnNames_ReflectsTransformedSchemaNames()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var names = transformer.ColumnNames;

        // Assert
        names.Should().BeEquivalentTo(["X (text)", "B (text)"], o => o.WithStrictOrdering());
    }

    // -------------------------------------------------------------------------
    // Filter — Equals
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_EqualsOperator_OnlyMatchingRowsReturned()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
                ["Alice"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
        ];

        // Act — rows 0 and 2 match "Alice"
        using var transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
        transformer[0, 0].Should().Be("Alice");
        transformer[1, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_ContainsOperator_SubstringMatchingRowsReturned()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["apple"],
                ["banana"],
                ["apricot"],
                ["cherry"],
            ],
            ["Fruit"]
        );
        var schema = MakeSchema(("Fruit", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Fruit", FilterOperator.Contains, ComparisonType.Text, "ap").Value,
        ];

        // Act — rows 0 ("apple") and 2 ("apricot") contain "ap"
        using var transformer = MakeFilteredTransformer(source, schema, actions, [0, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
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
        var source = new FakeTableSource(
            [
                ["Alice", "30"],
                ["Bob", "25"],
                ["Alice", "20"],
                ["Charlie", "30"],
            ],
            ["Name", "Age"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text), ("Age", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
            FilterAction.Create("Age", FilterOperator.Equals, ComparisonType.Text, "30").Value,
        ];

        // Act — only row 0 ("Alice", "30") matches both filters
        using var transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
        transformer[0, 1].Should().Be("30");
    }

    [Fact]
    public void Filter_NoMatchingRows_RowsIsZero()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Name", FilterOperator.Equals, ComparisonType.Text, "Charlie").Value,
        ];

        // Act — no rows match "Charlie"
        using var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert
        transformer.Rows.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Filter — column resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_TargetingRenamedColumn_CorrectlyResolved()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice"],
                ["Bob"],
            ],
            ["Name"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "Name", NewName = "FullName" },
            FilterAction.Create("FullName", FilterOperator.Equals, ComparisonType.Text, "Alice").Value,
        ];

        // Act — only row 0 ("Alice") matches
        using var transformer = MakeFilteredTransformer(source, schema, actions, [0]);

        // Assert
        transformer.Rows.Should().Be(1);
        transformer[0, 0].Should().Be("Alice");
    }

    [Fact]
    public void Filter_TargetingDeletedColumn_SilentlySkipped()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["Alice", "active"],
                ["Bob", "inactive"],
            ],
            ["Name", "Status"]
        );
        var schema = MakeSchema(("Name", ColumnType.Text), ("Status", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "Status" },
            // Filter targets a deleted column — should be silently skipped, no rows excluded
            FilterAction.Create("Status", FilterOperator.Equals, ComparisonType.Text, "active").Value,
        ];

        // Act — the filter spec is skipped (Status column was deleted), so no FilterSpec
        // is generated and the factory is never called; all source rows are exposed
        using var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — both rows retained because the filter was silently skipped
        transformer.Rows.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Filter — numeric operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Filter_GreaterThanOnWholeNumberColumn_ReturnsMatchingRows()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["10"],
                ["50"],
                ["30"],
                ["5"],
            ],
            ["Score"]
        );
        var schema = MakeSchema(("Score", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Score", FilterOperator.GreaterThan, ComparisonType.Number, "20").Value,
        ];

        // Act — rows 1 (50) and 2 (30) are greater than 20
        using var transformer = MakeFilteredTransformer(source, schema, actions, [1, 2]);

        // Assert
        transformer.Rows.Should().Be(2);
        transformer[0, 0].Should().Be("50");
        transformer[1, 0].Should().Be("30");
    }

    [Fact]
    public void Filter_NumericOperatorOnTextColumn_ExcludesAllRows()
    {
        // Arrange — GreaterThan on a Text column always returns false, excluding all rows
        var source = new FakeTableSource(
            [
                ["hello"],
                ["world"],
            ],
            ["Word"]
        );
        var schema = MakeSchema(("Word", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            FilterAction.Create("Word", FilterOperator.GreaterThan, ComparisonType.Number, "20").Value,
        ];

        // Act — numeric operators on Text columns always return false, so no rows match
        using var transformer = MakeFilteredTransformer(source, schema, actions, []);

        // Assert — all rows excluded because numeric operators are unsupported on Text columns
        transformer.Rows.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Fill
    // -------------------------------------------------------------------------

    [Fact]
    public void FillColumnAction_SingleColumn_AllCellsReturnFillValue()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1"],
                ["a2"],
                ["a3"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("FILLED");
        transformer[1, 0].Should().Be("FILLED");
        transformer[2, 0].Should().Be("FILLED");
    }

    [Fact]
    public void FillColumnAction_NonExistentColumn_IsIgnored()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "DoesNotExist", Value = "FILLED" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("a1");
    }

    [Fact]
    public void FillColumnAction_EmptyString_AllCellsReturnEmpty()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1"],
                ["a2"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = string.Empty },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be(string.Empty);
        transformer[1, 0].Should().Be(string.Empty);
    }

    [Fact]
    public void FillColumnAction_WithRename_FillAppliedAfterRename()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1", "b1"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
            new FillColumnAction { ColumnName = "X", Value = "FILLED" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer[0, 0].Should().Be("FILLED");
        transformer[0, 1].Should().Be("b1");
    }

    [Fact]
    public void FillColumnAction_SameColumnFilledTwice_LastValueWins()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["original"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "first" },
            new FillColumnAction { ColumnName = "A", Value = "second" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("second");
    }

    [Fact]
    public void FillColumnAction_MultipleColumns_OnlyTargetColumnFilled()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1", "b1", "c1"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "B", Value = "FILLED" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("a1");
        transformer[0, 1].Should().Be("FILLED");
        transformer[0, 2].Should().Be("c1");
    }

    [Fact]
    public void FillColumnAction_FillBeforeRename_FillPreservedAfterRename()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = "FILLED" },
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer[0, 0].Should().Be("FILLED");
    }

    [Fact]
    public void FillColumnAction_WithCast_FillValueBypassesCastFormatting()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["42"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
            new FillColumnAction { ColumnName = "A", Value = "hello" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        // FillValue takes precedence over cast formatting
        transformer[0, 0].Should().Be("hello");
    }

    [Fact]
    public void FillColumnAction_ColumnNames_PreservesAllColumnNames()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a1", "b1", "c1"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "B", Value = "FILLED" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        // All three columns should be present after fill action
        transformer.ColumnNames.Should().HaveCount(3);
        transformer.ColumnNames[0].Should().Be("A (text)");
        transformer.ColumnNames[1].Should().Be("B (text)");
        transformer.ColumnNames[2].Should().Be("C (text)");
        transformer.Columns.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // RawColumnNames — unlabeled raw names
    // -------------------------------------------------------------------------

    [Fact]
    public void RawColumnNames_EmptyActionStack_ReturnsSchemaNames()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["42", "hello"],
            ],
            ["Age", "Name"]
        );
        var schema = MakeSchema(("Age", ColumnType.WholeNumber), ("Name", ColumnType.Text));

        // Act
        using var transformer = new LazyTransformer(source, schema, []);

        // Assert
        transformer.RawColumnNames[0].Should().Be("Age");
        transformer.RawColumnNames[1].Should().Be("Name");
    }

    [Fact]
    public void RawColumnNames_WithRenameAction_ReflectsNewName()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        var schema = MakeSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.RawColumnNames[0].Should().Be("X");
        transformer.RawColumnNames[1].Should().Be("B");
    }

    [Fact]
    public void RawColumnNames_WithDeleteAction_ExcludesDeletedColumn()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        var schema = MakeSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<MorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.RawColumnNames.Should().BeEquivalentTo(["A", "C"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void ColumnNames_WithCastAction_ReflectsNewTypeLabel()
    {
        // Arrange
        var source = new FakeTableSource([["42"]], ["A"]);
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("A (number)");
        transformer.RawColumnNames[0].Should().Be("A");
    }

    [Fact]
    public void ColumnNames_EmptyActionStack_ReturnsLabeledNames()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["42", "hello"],
            ],
            ["Age", "Name"]
        );
        var schema = MakeSchema(("Age", ColumnType.WholeNumber), ("Name", ColumnType.Text));

        // Act
        using var transformer = new LazyTransformer(source, schema, []);

        // Assert
        transformer.ColumnNames[0].Should().Be("Age (number)");
        transformer.ColumnNames[1].Should().Be("Name (text)");
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
        var source = new FakeTableSource(
            [
                ["original"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "A", Value = fillValue },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be($"A ({expectedLabel})");
        transformer.RawColumnNames[0].Should().Be("A");
    }

    [Fact]
    public void FillColumnAction_NumberColumnFilledWithText_TypeChangesToText()
    {
        // Arrange
        var source = new FakeTableSource([["100"]], ["Price"]);
        var schema = MakeSchema(("Price", ColumnType.WholeNumber));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "Price", Value = "N/A" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("Price (text)");
        transformer[0, 0].Should().Be("N/A");
    }

    [Fact]
    public void FillColumnAction_TextColumnFilledWithNumber_TypeChangesToNumber()
    {
        // Arrange
        var source = new FakeTableSource([["hello"]], ["Value"]);
        var schema = MakeSchema(("Value", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FillColumnAction { ColumnName = "Value", Value = "42" },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("Value (number)");
        transformer[0, 0].Should().Be("42");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — FormatTimestamp
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatTimestampAction_OnTimestampColumn_FormatsValueWithTargetFormat()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
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
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("2024/01/15");
    }

    [Fact]
    public void FormatTimestampAction_OnNonExistentColumn_IsSkipped()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["hello"],
            ],
            ["A"]
        );
        var schema = MakeSchema(("A", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "DoesNotExist",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("hello");
    }

    [Fact]
    public void FormatTimestampAction_AfterCastToTimestamp_FormatsCorrectly()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["2024-01-15"],
            ],
            ["created_at"]
        );
        var schema = MakeSchema(("created_at", ColumnType.Text));
        IReadOnlyList<MorphAction> actions =
        [
            new CastColumnAction { ColumnName = "created_at", TargetType = ColumnType.Timestamp },
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = "yyyy/MM/dd",
            },
        ];

        // Act
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("2024/01/15");
    }

    [Fact]
    public void MultipleFormatTimestampActions_LastOneWins()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
        );
        var schema = MakeSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<MorphAction> actions =
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
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("01/15/2024");
    }

    [Fact]
    public void FormatTimestampAction_WithInvalidTimestampValue_ReturnsInvalidMarker()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["not-a-date"],
            ],
            ["created_at"]
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
        using var transformer = new LazyTransformer(source, schema, actions);

        // Assert
        transformer[0, 0].Should().Be("<invalid>");
    }

    [Fact]
    public void FormatTimestampAction_WithEmptyTargetFormat_UsesDefaultFormat()
    {
        // Arrange
        var source = new FakeTableSource(
            [
                ["2024-01-15T09:30:00"],
            ],
            ["created_at"]
        );
        var schema = MakeSchema(("created_at", ColumnType.Timestamp));
        IReadOnlyList<MorphAction> actions =
        [
            new FormatTimestampAction
            {
                ColumnName = "created_at",
                TargetFormat = string.Empty,
            },
        ];
        using var transformer = new LazyTransformer(source, schema, actions);

        // Act
        var result = transformer[0, 0];

        // Assert
        result.Should().Be("2024-01-15 09:30:00");
    }

    [Fact]
    public void Dispose_DisposesUnderlyingSource()
    {
        // Arrange
        using var source = new DisposableFakeTableSource([["a"]], ["A"]);
        var schema = MakeSchema(("A", ColumnType.Text));
        var transformer = new LazyTransformer(source, schema, []);

        // Act
        transformer.Dispose();

        // Assert
        source.IsDisposed.Should().BeTrue();
    }
}
