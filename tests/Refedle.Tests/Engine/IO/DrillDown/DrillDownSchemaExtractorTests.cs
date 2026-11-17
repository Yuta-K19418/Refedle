using AwesomeAssertions;
using Refedle.Engine;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.DrillDown;

public sealed class DrillDownSchemaExtractorTests
{
    [Fact]
    public void ExtractFromNode_ArrayOfObjects_ReturnsUnionOfAllTopLevelKeys()
    {
        // Arrange
        JsonRawBytes nodeBytes =
            "[{\"name\": \"Alice\", \"age\": 30}, {\"name\": \"Bob\", \"age\": 25}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("name", "age");
        result.Value.childRawValues.Should().HaveCount(2);
    }

    [Fact]
    public void ExtractFromNode_ArrayOfObjectsWithVaryingKeys_ReturnsNullableColumnsForMissingKeys()
    {
        // Arrange
        JsonRawBytes nodeBytes =
            "[{\"name\": \"Alice\", \"role\": \"admin\"}, {\"name\": \"Bob\"}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.schema.Columns.Single(c => c.Name == "role").IsNullable.Should().BeTrue();
        result.Value.schema.Columns.Single(c => c.Name == "name").IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ExtractFromNode_ArrayWithNonObjectElement_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[{\"name\": \"Alice\"}, 42]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExtractFromNode_EmptyArray_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExtractFromNode_ArrayOfEmptyObjects_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[{}, {}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All child objects have no keys");
    }

    [Fact]
    public void ExtractFromNode_MalformedJson_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[invalid]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExtractFromNode_SourceFormatMatchesParameter_ReturnsCorrectFormat()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[{\"key\": \"value\"}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonArray);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.schema.SourceFormat.Should().Be(DataFormat.JsonArray);
    }

    [Fact]
    public void ExtractFromNode_NullBeforeNonNull_InfersCorrectType()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[{\"age\": null}, {\"age\": 30}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ColumnSchema ageColumn = result.Value.schema.Columns.Single(c => c.Name == "age");
        ageColumn.Type.Should().Be(ColumnType.WholeNumber);
        ageColumn.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void ExtractFromNode_JsonObjectInput_ReturnsFailure()
    {
        // Arrange
        JsonRawBytes nodeBytes = "{\"key\": \"value\"}"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ExtractFromNode_KeyWithAllNullValues_IsNullableWithTextType()
    {
        // Arrange
        JsonRawBytes nodeBytes =
            "[{\"name\": \"Alice\", \"tag\": null}, {\"name\": \"Bob\", \"tag\": null}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ColumnSchema tagColumn = result.Value.schema.Columns.Single(c => c.Name == "tag");
        tagColumn.IsNullable.Should().BeTrue();
        tagColumn.Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void ExtractFromNode_KeyWithMixedNullAndNonNull_IsNullable()
    {
        // Arrange
        JsonRawBytes nodeBytes = "[{\"name\": null}, {\"name\": \"Bob\"}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ColumnSchema nameColumn = result.Value.schema.Columns.Single(c => c.Name == "name");
        nameColumn.IsNullable.Should().BeTrue();
        nameColumn.Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void ExtractFromNode_ChildWithNestedObject_ReturnsCorrectChildBytesCount()
    {
        // Arrange
        JsonRawBytes nodeBytes =
            "[{\"name\": \"Alice\", \"meta\": {\"dept\": \"Eng\"}}, {\"name\": \"Bob\", \"meta\": {\"dept\": \"HR\"}}]"u8.ToArray();

        // Act
        Result<(TableSchema schema, IReadOnlyList<JsonRawBytes> childRawValues)> result = DrillDownSchemaExtractor.ExtractFromNode(nodeBytes, DataFormat.JsonLines);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.childRawValues.Should().HaveCount(2);
        result.Value.schema.Columns.Select(c => c.Name).Should().Equal("name", "meta");
    }
}
