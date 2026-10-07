using AwesomeAssertions;
using Refedle.App.Tui.Workers.Schema;
using Refedle.Engine;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui.Workers.Schema;

public sealed class BackgroundSchemaScanTests
{
    private const string FailingRow = "bad";

    private static TableSchema CreateInitialSchema()
    {
        return new TableSchema
        {
            Columns = [new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber }],
            SourceFormat = DataFormat.Csv,
        };
    }

    // Appends a column named after the row, so the result shows which rows were refined and in what order.
    private static Result<TableSchema> AppendColumnNamedAfterRow(TableSchema schema, string row)
    {
        if (row == FailingRow)
        {
            return Results.Failure<TableSchema>("refine failed");
        }

        var refinedSchema = schema with
        {
            Columns = [.. schema.Columns, new ColumnSchema { Name = row, Type = ColumnType.Text }],
        };
        return Results.Success(refinedSchema);
    }

    private static string[] ColumnNames(TableSchema schema)
    {
        return [.. schema.Columns.Select(static column => column.Name)];
    }

    private static IEnumerable<string> CancelBeforeYieldingRow(CancellationTokenSource cts, string row)
    {
        cts.Cancel();
        yield return row;
    }

    private sealed class TrackedRows(params string[] rows)
    {
        public bool IsEnumeratorDisposed { get; private set; }

        public IEnumerable<string> Read()
        {
            try
            {
                foreach (var row in rows)
                {
                    yield return row;
                }
            }
            finally
            {
                IsEnumeratorDisposed = true;
            }
        }
    }

    [Fact]
    public void Execute_WithAllRowsRefinable_ReadsRowsOnceAndReturnsSchemaRefinedWithEveryRow()
    {
        // Arrange
        var trackedRows = new TrackedRows("a", "b", "c");
        var readRowsCallCount = 0;

        // Act
        var schema = BackgroundSchemaScan.Execute(
            CreateInitialSchema(),
            () =>
            {
                readRowsCallCount++;
                return trackedRows.Read();
            },
            AppendColumnNamedAfterRow,
            CancellationToken.None);

        // Assert
        ColumnNames(schema).Should().Equal("id", "a", "b", "c");
        readRowsCallCount.Should().Be(1);
    }

    [Fact]
    public void Execute_WithNoRows_ReturnsCurrentSchema()
    {
        // Arrange
        var currentSchema = CreateInitialSchema();
        var trackedRows = new TrackedRows();

        // Act
        var schema = BackgroundSchemaScan.Execute(
            currentSchema,
            trackedRows.Read,
            AppendColumnNamedAfterRow,
            CancellationToken.None);

        // Assert
        schema.Should().BeSameAs(currentSchema);
    }

    [Fact]
    public void Execute_WhenRefineFailsForARow_IgnoresThatRowAndKeepsRefiningOthers()
    {
        // Arrange
        var trackedRows = new TrackedRows("a", FailingRow, "c");

        // Act
        var schema = BackgroundSchemaScan.Execute(
            CreateInitialSchema(),
            trackedRows.Read,
            AppendColumnNamedAfterRow,
            CancellationToken.None);

        // Assert
        ColumnNames(schema).Should().Equal("id", "a", "c");
    }

    [Fact]
    public void Execute_WhenCancelledBeforeStart_ReturnsCurrentSchemaWithoutCallingReadRows()
    {
        // Arrange
        var currentSchema = CreateInitialSchema();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var readRowsCallCount = 0;

        // Act
        var schema = BackgroundSchemaScan.Execute<string>(
            currentSchema,
            () =>
            {
                readRowsCallCount++;
                return ["a"];
            },
            AppendColumnNamedAfterRow,
            cts.Token);

        // Assert
        schema.Should().BeSameAs(currentSchema);
        readRowsCallCount.Should().Be(0);
    }

    [Fact]
    public void Execute_WhenCancelledAfterRowIsAcquired_DoesNotRefineThatRow()
    {
        // Arrange
        var currentSchema = CreateInitialSchema();
        using var cts = new CancellationTokenSource();

        // Act
        var schema = BackgroundSchemaScan.Execute(
            currentSchema,
            () => CancelBeforeYieldingRow(cts, "a"),
            AppendColumnNamedAfterRow,
            cts.Token);

        // Assert
        schema.Should().BeSameAs(currentSchema);
    }

    [Fact]
    public void Execute_WhenCancelledWhileRefining_DoesNotRefineLaterRows()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var trackedRows = new TrackedRows("a", "b", "c", "d");

        // Act
        var schema = BackgroundSchemaScan.Execute(
            CreateInitialSchema(),
            trackedRows.Read,
            (currentSchema, row) =>
            {
                var refineResult = AppendColumnNamedAfterRow(currentSchema, row);
                cts.Cancel();
                return refineResult;
            },
            cts.Token);

        // Assert
        ColumnNames(schema).Should().Equal("id", "a");
    }

    [Fact]
    public void Execute_WhenCancelledWhileRefining_DisposesRowEnumerator()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var trackedRows = new TrackedRows("a", "b", "c");

        // Act
        _ = BackgroundSchemaScan.Execute(
            CreateInitialSchema(),
            trackedRows.Read,
            (currentSchema, row) =>
            {
                cts.Cancel();
                return AppendColumnNamedAfterRow(currentSchema, row);
            },
            cts.Token);

        // Assert
        trackedRows.IsEnumeratorDisposed.Should().BeTrue();
    }

    [Fact]
    public void Execute_WhenAllRowsAreRead_DisposesRowEnumerator()
    {
        // Arrange
        var trackedRows = new TrackedRows("a", "b");

        // Act
        _ = BackgroundSchemaScan.Execute(
            CreateInitialSchema(),
            trackedRows.Read,
            AppendColumnNamedAfterRow,
            CancellationToken.None);

        // Assert
        trackedRows.IsEnumeratorDisposed.Should().BeTrue();
    }
}
