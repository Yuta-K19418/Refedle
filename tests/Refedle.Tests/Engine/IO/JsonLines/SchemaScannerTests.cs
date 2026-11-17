using System.Text.Json;
using AwesomeAssertions;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    private static JsonRawBytes Line(string json) =>
        new(
            JsonSerializer.SerializeToUtf8Bytes(JsonElement.Parse(json))
        );

    private static void AssertColumn(
        TableSchema schema,
        int index,
        string expectedName,
        ColumnType expectedType,
        bool expectedNullable
    )
    {
        schema.Columns.Should().HaveCountGreaterThan(index);
        ColumnSchema column = schema.Columns[index];
        column.Name.Should().Be(expectedName);
        column.Type.Should().Be(expectedType);
        column.IsNullable.Should().Be(expectedNullable);
    }
}
