using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Refedle.TestHelpers.IO;
using static Refedle.Tests.App.Tui.UI.TestSchemas;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class LazyTransformerTests
{
    [Fact]
    public void CastAction_ToTimestamp_WithoutFormatTimestamp_UsesDefaultFormat()
    {
        // Arrange
        StringRowsFakeTableSource source = new(
            [
                ["2024-03-20T14:05:33"],
            ],
            ["updated_at"]
        );
        TableSchema schema = MakeCsvSchema(("updated_at", ColumnType.Text));
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "updated_at", TargetType = ColumnType.Timestamp },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("2024-03-20 14:05:33");
    }

    [Fact]
    public void CastAction_AfterFormatTimestamp_DoesNotResetFormatString()
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
            new CastColumnAction { ColumnName = "created_at", TargetType = ColumnType.Timestamp },
            new FormatTimestampAction { ColumnName = "created_at", TargetFormat = "yyyy/MM/dd" },
            new CastColumnAction { ColumnName = "created_at", TargetType = ColumnType.Timestamp },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object result = transformer[0, 0];

        // Assert
        result.Should().Be("2024/01/15");
    }

    [Fact]
    public void CastAction_OverJsonLinesNumberColumn_ReformatsCastColumnAndLeavesOtherRaw()
    {
        // Arrange
        using TempFile tempFile = TempFile.CreateEmpty(".tmp");
        string path = tempFile.Path;
        File.WriteAllLines(path, ["{\"price\": 1.50, \"amount\": 1.50}"]);
        RowIndexer indexer = new(path);
        indexer.BuildIndex();
        using RowByteCache cache = new(indexer);
        TableSchema schema = MakeCsvSchema(
            ("price", ColumnType.FloatingPoint),
            ("amount", ColumnType.FloatingPoint)
        );
        // The transformer disposes its wrapped source; the using guards the
        // transformer-construction gap (double dispose is a guarded no-op).
        using JsonLinesTableSource source = new(cache, schema);
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "price", TargetType = ColumnType.FloatingPoint },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object cast = transformer[0, 0];
        object untouched = transformer[0, 1];

        // Assert
        cast.Should().Be("1.5");
        untouched.Should().Be("1.50");
    }

    [Fact]
    public void CastAction_OverJsonLinesNumberColumn_ToWholeNumber_ShowsInvalidForRawNotations()
    {
        // Arrange
        using TempFile tempFile = TempFile.CreateEmpty(".tmp");
        string path = tempFile.Path;
        File.WriteAllLines(path, ["{\"exponent\": 1e3, \"integral\": 1.0, \"plain\": 1000}"]);
        RowIndexer indexer = new(path);
        indexer.BuildIndex();
        using RowByteCache cache = new(indexer);
        // Schema types mirror the schema scan's inference: "1e3" -> Text,
        // "1.0" -> FloatingPoint, "1000" -> WholeNumber.
        TableSchema schema = MakeCsvSchema(
            ("exponent", ColumnType.Text),
            ("integral", ColumnType.FloatingPoint),
            ("plain", ColumnType.WholeNumber)
        );
        // The transformer disposes its wrapped source; the using guards the
        // transformer-construction gap (double dispose is a guarded no-op).
        using JsonLinesTableSource source = new(cache, schema);
        IReadOnlyList<IMorphAction> actions =
        [
            new CastColumnAction { ColumnName = "exponent", TargetType = ColumnType.WholeNumber },
            new CastColumnAction { ColumnName = "integral", TargetType = ColumnType.WholeNumber },
            new CastColumnAction { ColumnName = "plain", TargetType = ColumnType.WholeNumber },
        ];
        using LazyTransformer transformer = LazyTransformer.Create(source, schema, actions);

        // Act
        object exponent = transformer[0, 0];
        object integral = transformer[0, 1];
        object plain = transformer[0, 2];

        // Assert — integral values in raw exponent/decimal notation fail long parsing;
        // plain integers still display normally
        exponent.Should().Be("<invalid>");
        integral.Should().Be("<invalid>");
        plain.Should().Be("1000");
    }
}
