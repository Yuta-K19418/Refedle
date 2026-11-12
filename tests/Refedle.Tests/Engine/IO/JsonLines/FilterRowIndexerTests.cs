using AwesomeAssertions;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.Engine.IO.JsonLines;

public sealed class FilterRowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public FilterRowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlinesFilterRowIndexer_{Guid.NewGuid()}.jsonl"
        );
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public async Task BuildIndexAsync_WholeNumberFilterGreaterThan_ExcludesRawExponentAndDecimalNotation()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.WholeNumber, FilterOperator.GreaterThan, "999");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1e3}", "{\"value\": 1000}", "{\"value\": 1.0}"],
            spec
        );

        // Assert — raw "1e3" and "1.0" fail long parsing; only the plain "1000" row matches
        filterIndexer.TotalMatchedRows.Should().Be(1);
        filterIndexer.GetSourceRow(0).Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_WholeNumberFilterGreaterThanOrEqual_ExcludesRawDecimalNotation()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.WholeNumber, FilterOperator.GreaterThanOrEqual, "1");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1.0}", "{\"value\": 1}"],
            spec
        );

        // Assert — raw "1.0" fails long parsing even though it is integral; only "1" matches
        filterIndexer.TotalMatchedRows.Should().Be(1);
        filterIndexer.GetSourceRow(0).Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_TextEqualsFilter_MatchesRawTokenText()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1.50");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(["{\"value\": 1.50}"], spec);

        // Assert — string operators compare against the raw token text
        filterIndexer.TotalMatchedRows.Should().Be(1);
    }

    [Fact]
    public async Task BuildIndexAsync_TextEqualsFilter_NormalizedText_DoesNotMatchRawToken()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.Text, FilterOperator.Equals, "1.5");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(["{\"value\": 1.50}"], spec);

        // Assert — the normalized spelling no longer matches the raw "1.50" token
        filterIndexer.TotalMatchedRows.Should().Be(0);
    }

    [Fact]
    public async Task BuildIndexAsync_FloatingPointFilter_ComparesRawTokensNumerically()
    {
        // Arrange
        FilterSpec spec = new(0, ColumnType.FloatingPoint, FilterOperator.GreaterThan, "1.4");

        // Act
        var filterIndexer = await BuildFilterIndexAsync(
            ["{\"value\": 1.50}", "{\"value\": 1.4}", "{\"value\": 1e3}"],
            spec
        );

        // Assert — "1.50" and "1e3" still compare numerically; the boundary "1.4" is excluded
        filterIndexer.TotalMatchedRows.Should().Be(2);
        filterIndexer.GetSourceRow(0).Should().Be(0);
        filterIndexer.GetSourceRow(1).Should().Be(2);
    }

    private async Task<FilterRowIndexer> BuildFilterIndexAsync(
        IReadOnlyList<string> lines,
        FilterSpec spec
    )
    {
        await File.WriteAllLinesAsync(_testFilePath, lines);
        var rowIndexer = new RowIndexer(_testFilePath);
        rowIndexer.BuildIndex();
        var filterIndexer = new FilterRowIndexer(
            rowIndexer,
            _testFilePath,
            ["value"u8.ToArray()],
            [spec]
        );
        await filterIndexer.BuildIndexAsync(CancellationToken.None);
        return filterIndexer;
    }
}
