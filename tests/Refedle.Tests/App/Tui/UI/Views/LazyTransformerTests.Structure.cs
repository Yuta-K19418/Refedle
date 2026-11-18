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
    // Create — null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));

        // Act
        Func<LazyTransformer> act = () => LazyTransformer.Create(null!, schema, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOriginalSchema_ThrowsArgumentNullException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello"],
            ],
            ["A"]
        );

        // Act
        Func<LazyTransformer> act = () => LazyTransformer.Create(source, null!, []);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullActions_ThrowsArgumentNullException()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));

        // Act
        Func<LazyTransformer> act = () => LazyTransformer.Create(source, schema, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Rename
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithRenameAction_OutputSchemaReflectsNewName()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("X (text)");
        transformer.ColumnNames[1].Should().Be("B (text)");
    }

    [Fact]
    public void Create_WithRenameAction_SourceColumnIndexPreserved()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("hello");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Delete
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithDeleteAction_DeletedColumnAbsentFromOutputSchema()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(2);
        transformer.ColumnNames.Should().BeEquivalentTo(["A (text)", "C (text)"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Create_WithDeleteAction_SourceColumnIndicesMappedCorrectly()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 1]; // output col 1 → source col 2 (C)

        // Assert
        result.Should().Be("c");
    }

    [Fact]
    public void Create_AllColumnsDeleted_ColumnsIsZero()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions = [new DeleteColumnAction { ColumnName = "A" }];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Cast
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithCastAction_OutputSchemaReflectsNewType()
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
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        // ColumnType is reflected through FormatCellValue behaviour: valid integer is returned as-is
        transformer[0, 0].Should().Be("42");
    }

    // -------------------------------------------------------------------------
    // Schema transformation — Ordered actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithRenameFollowedByDelete_OperatesOnRenamedName()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
            new DeleteColumnAction { ColumnName = "X" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
        transformer.ColumnNames[0].Should().Be("B (text)");
    }

    // -------------------------------------------------------------------------
    // Error handling — silently skipped actions
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ActionTargetingNonExistentColumn_IsSilentlySkipped()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new DeleteColumnAction { ColumnName = "DoesNotExist" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
    }

    [Fact]
    public void Create_CastTargetingNonExistentColumn_IsSkippedAndOtherColumnsUnchanged()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["007"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "DoesNotExist", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.Columns.Should().Be(1);
        transformer.ColumnNames[0].Should().Be("A (text)");
        transformer[0, 0].Should().Be("007");
    }

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    [Fact]
    public void Rows_DelegatesToUnderlyingSource()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a"],
                ["b"],
                ["c"],
                ["d"],
                ["e"],
            ],
            ["A"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        long rows = transformer.TotalRows;

        // Assert
        rows.Should().Be(5);
    }

    [Fact]
    public void Columns_ReflectsTransformedSchemaColumnCount()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        int columns = transformer.Columns;

        // Assert
        columns.Should().Be(2);
    }

    [Fact]
    public void ColumnNames_ReflectsTransformedSchemaNames()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        string[] names = transformer.ColumnNames;

        // Assert
        names.Should().BeEquivalentTo(["X (text)", "B (text)"], o => o.WithStrictOrdering());
    }

    // -------------------------------------------------------------------------
    // RawColumnNames — unlabeled raw names
    // -------------------------------------------------------------------------

    [Fact]
    public void RawColumnNames_EmptyActionStack_ReturnsSchemaNames()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["42", "hello"],
            ],
            ["Age", "Name"]
        );
        TableSchema schema = MakeCsvSchema(("Age", ColumnType.WholeNumber), ("Name", ColumnType.Text));

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Assert
        transformer.RawColumnNames[0].Should().Be("Age");
        transformer.RawColumnNames[1].Should().Be("Name");
    }

    [Fact]
    public void RawColumnNames_WithRenameAction_ReflectsNewName()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["hello", "world"],
            ],
            ["A", "B"]
        );
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text), ("B", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new RenameColumnAction { OldName = "A", NewName = "X" },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.RawColumnNames[0].Should().Be("X");
        transformer.RawColumnNames[1].Should().Be("B");
    }

    [Fact]
    public void RawColumnNames_WithDeleteAction_ExcludesDeletedColumn()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["a", "b", "c"],
            ],
            ["A", "B", "C"]
        );
        TableSchema schema = MakeCsvSchema(
            ("A", ColumnType.Text),
            ("B", ColumnType.Text),
            ("C", ColumnType.Text)
        );
        IReadOnlyList<IMorphAction> actions = [new DeleteColumnAction { ColumnName = "B" }];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.RawColumnNames.Should().BeEquivalentTo(["A", "C"], o => o.WithStrictOrdering());
    }

    [Fact]
    public void ColumnNames_WithCastAction_ReflectsNewTypeLabel()
    {
        // Arrange
        StringRowsFakeTableSource source = new([["42"]], ["A"]);
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "A", TargetType = ColumnType.WholeNumber },
        ];

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Assert
        transformer.ColumnNames[0].Should().Be("A (number)");
        transformer.RawColumnNames[0].Should().Be("A");
    }

    [Fact]
    public void ColumnNames_EmptyActionStack_ReturnsLabeledNames()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["42", "hello"],
            ],
            ["Age", "Name"]
        );
        TableSchema schema = MakeCsvSchema(("Age", ColumnType.WholeNumber), ("Name", ColumnType.Text));

        // Act
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Assert
        transformer.ColumnNames[0].Should().Be("Age (number)");
        transformer.ColumnNames[1].Should().Be("Name (text)");
    }

    [Fact]
    public void Dispose_DisposesUnderlyingSource()
    {
        // Arrange
        using StringRowsFlagDisposableTableSource source = new([["a"]], ["A"]);
        TableSchema schema = MakeCsvSchema(("A", ColumnType.Text));
        LazyTransformer transformer = LazyTransformer.Create(source, schema, []);

        // Act
        transformer.Dispose();

        // Assert
        source.IsDisposed.Should().BeTrue();
    }
}
