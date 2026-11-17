using AwesomeAssertions;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.Csv;

public sealed partial class SchemaScannerTests
{
    private static void AssertColumn(
        TableSchema schema,
        int index,
        string expectedName,
        ColumnType expectedType,
        bool expectedNullable
    )
    {
        ColumnSchema column = schema.Columns[index];
        column.Name.Should().Be(expectedName);
        column.Type.Should().Be(expectedType);
        column.IsNullable.Should().Be(expectedNullable);
        column.ColumnIndex.Should().Be(index);
    }
}
