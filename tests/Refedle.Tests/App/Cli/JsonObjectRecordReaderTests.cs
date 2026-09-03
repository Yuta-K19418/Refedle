using System.Text;
using AwesomeAssertions;
using Refedle.App.Cli;
using Refedle.Engine;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Cli;

public sealed class JsonObjectRecordReaderTests
{
    private const string ColumnName = "value";

    private static JsonRawBytes Row(string rowJson) => Encoding.UTF8.GetBytes(rowJson);

    [Fact]
    public async Task MoveNextAsync_YieldsAllRowsInOrderThenStops()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":"a"}"""), Row("""{"value":"b"}"""), Row("""{"value":"c"}""") };
        var (inputColumnNames, outputSchema) = BuildSchemas();
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);

        // Act
        var first = await reader.MoveNextAsync(default);
        var firstValue = reader.GetCellData(0).Value.ToString();
        var second = await reader.MoveNextAsync(default);
        var secondValue = reader.GetCellData(0).Value.ToString();
        var third = await reader.MoveNextAsync(default);
        var thirdValue = reader.GetCellData(0).Value.ToString();
        var fourth = await reader.MoveNextAsync(default);

        // Assert
        first.Should().BeTrue();
        firstValue.Should().Be("a");
        second.Should().BeTrue();
        secondValue.Should().Be("b");
        third.Should().BeTrue();
        thirdValue.Should().Be("c");
        fourth.Should().BeFalse();
    }

    [Fact]
    public async Task MoveNextAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":1}""") };
        var (inputColumnNames, outputSchema) = BuildSchemas();
        var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        reader.Dispose();

        // Act
        var act = async () => await reader.MoveNextAsync(default);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task MoveNextAsync_WithNoRows_ReturnsFalse()
    {
        // Arrange
        JsonRawBytes[] rows = [];
        var (inputColumnNames, outputSchema) = BuildSchemas();
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);

        // Act
        var hasNext = await reader.MoveNextAsync(default);

        // Assert
        hasNext.Should().BeFalse();
    }

    [Fact]
    public async Task MoveNextAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":1}""") };
        var (inputColumnNames, outputSchema) = BuildSchemas();
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await reader.MoveNextAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateFilters_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":1}""") };
        var (inputColumnNames, outputSchema) = BuildSchemas();
        var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);
        reader.Dispose();

        // Act
        var act = () => reader.EvaluateFilters();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetCellData_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":1}""") };
        var (inputColumnNames, outputSchema) = BuildSchemas();
        var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        reader.Dispose();

        // Act
        Action act = () => { _ = reader.GetCellData(0); };

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task EvaluateFilters_MatchingStringFilter_ReturnsTrue()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":"hello"}""") };
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "hello")];
        var (inputColumnNames, outputSchema) = BuildSchemas([ColumnName], filters);
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);

        // Act
        var result = reader.EvaluateFilters();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateFilters_NonMatchingStringFilter_ReturnsFalse()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":"hello"}""") };
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "world")];
        var (inputColumnNames, outputSchema) = BuildSchemas([ColumnName], filters);
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);

        // Act
        var result = reader.EvaluateFilters();

        // Assert
        result.Should().BeFalse();
    }

    // JSON null extracts to the "<null>" sentinel, which the reader rejects.
    [Fact]
    public async Task EvaluateFilters_NullJsonValue_ReturnsFalse()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":null}""") };
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
        var (inputColumnNames, outputSchema) = BuildSchemas([ColumnName], filters);
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);

        // Act
        var result = reader.EvaluateFilters();

        // Assert
        result.Should().BeFalse();
    }

    // A non-object row extracts to the "<error>" sentinel, which the reader rejects.
    [Fact]
    public async Task EvaluateFilters_MalformedRow_ReturnsFalse()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("not valid json") };
        BatchFilterSpec[] filters = [new(0, ComparisonType.Text, FilterOperator.Equals, "anything")];
        var (inputColumnNames, outputSchema) = BuildSchemas([ColumnName], filters);
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);

        // Act
        var result = reader.EvaluateFilters();

        // Assert
        result.Should().BeFalse();
    }

    // A filter whose source column index is absent from the input schema is ignored, not rejected.
    [Fact]
    public async Task EvaluateFilters_FilterColumnAbsentFromInputSchema_IgnoresFilter()
    {
        // Arrange
        var rows = new JsonRawBytes[] { Row("""{"value":1}""") };
        BatchFilterSpec[] filters = [new(9, ComparisonType.Text, FilterOperator.Equals, "anything")];
        var (inputColumnNames, outputSchema) = BuildSchemas([ColumnName], filters);
        using var reader = new JsonObjectRecordReader(rows, inputColumnNames, outputSchema);
        await reader.MoveNextAsync(default);

        // Act
        var result = reader.EvaluateFilters();

        // Assert
        result.Should().BeTrue();
    }

    private static (IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema) BuildSchemas() =>
        BuildSchemas([ColumnName]);

    private static (IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema) BuildSchemas(IReadOnlyList<string> columnNames)
        => BuildSchemas(columnNames, []);

    private static (IReadOnlyList<string> inputColumnNames, BatchOutputSchema outputSchema) BuildSchemas(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<BatchFilterSpec> filters)
    {
        var outputSchema = new BatchOutputSchema([.. columnNames.Select(name => new BatchOutputColumn(name, name))], filters);
        return (columnNames, outputSchema);
    }
}
