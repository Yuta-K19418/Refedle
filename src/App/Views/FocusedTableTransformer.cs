using System.Globalization;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.App.Views;

/// <summary>
/// <see cref="LazyTransformerBase"/> subclass for DrillDown results backed by a fully
/// materialized <see cref="FocusedTableSource"/>. Unlike <see cref="LazyTransformer"/>,
/// it has no <see cref="IFilterRowIndexer"/> — every row is already in memory, so
/// <see cref="FilterAction"/>s are resolved synchronously in <see cref="Create"/> via the
/// stateless <see cref="FilterEvaluator"/>.
/// Column 0 is the <c>"#"</c> pseudo column (a per-row hash), which is never a Morph
/// target: <see cref="Create"/> runs <see cref="LazyTransformerBase.BuildTransformedSchema"/>
/// over the DrillDown schema only, then prepends a fixed <c>"#"</c> slot to every output
/// array. Use <see cref="Create"/> to construct.
/// </summary>
internal sealed class FocusedTableTransformer(
    ITableSource source,
    IReadOnlyList<int>? matchedRowIndices,
    string[] columnNames,
    string[] rawColumnNames,
    IReadOnlyList<ColumnType> columnTypes,
    IReadOnlyList<int> sourceColumnIndices,
    IReadOnlyList<string?> fillValues,
    IReadOnlyList<string?> formatStrings
) : LazyTransformerBase(
    source,
    columnNames,
    rawColumnNames,
    columnTypes,
    sourceColumnIndices,
    fillValues,
    formatStrings
)
{
    private readonly IReadOnlyList<int>? _matchedRowIndices = matchedRowIndices;

    /// <summary>
    /// Creates a new <see cref="FocusedTableTransformer"/> by applying the action stack
    /// to derive the output column mapping, resolving any filters synchronously.
    /// </summary>
    /// <param name="source">The <see cref="FocusedTableSource"/>-shaped source whose column 0 is the <c>"#"</c> hash column.</param>
    /// <param name="originalSchema">The DrillDown schema before any actions are applied (no <c>"#"</c> entry).</param>
    /// <param name="actions">The ordered list of transformation actions to apply.</param>
    public static FocusedTableTransformer Create(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(originalSchema);
        ArgumentNullException.ThrowIfNull(actions);

        var schema = BuildTransformedSchema(originalSchema, actions);

        string[] columnNames = ["#", .. schema.columnNames];
        string[] rawColumnNames = ["#", .. schema.rawColumnNames];
        IReadOnlyList<ColumnType> columnTypes = [ColumnType.Text, .. schema.columnTypes];
        IReadOnlyList<int> sourceColumnIndices = [-1, .. schema.sourceColumnIndices];
        IReadOnlyList<string?> fillValues = [null, .. schema.fillValues];
        IReadOnlyList<string?> formatStrings = [null, .. schema.formatStrings];

        var matchedRowIndices = schema.filterSpecs.Count > 0
            ? ResolveMatchedRows(source, schema.filterSpecs)
            : null;

        return new FocusedTableTransformer(
            source,
            matchedRowIndices,
            columnNames,
            rawColumnNames,
            columnTypes,
            sourceColumnIndices,
            fillValues,
            formatStrings
        );
    }

    /// <summary>
    /// Resolves the source row indices matching every filter spec.
    /// AND semantics across all specs, matching <see cref="IFilterRowIndexer"/> (Csv/JsonLines).
    /// </summary>
    private static List<int> ResolveMatchedRows(
        ITableSource source,
        IReadOnlyList<FilterSpec> filterSpecs
    )
    {
        List<int> matched = [];
        for (var row = 0; row < source.Rows; row++)
        {
            var isMatch = true;
            foreach (var spec in filterSpecs)
            {
                var rawValue = Convert.ToString(
                    source[row, spec.SourceColumnIndex + 1],
                    CultureInfo.InvariantCulture
                ) ?? string.Empty;
                isMatch = FilterEvaluator.EvaluateFilter(rawValue.AsSpan(), spec);
                if (!isMatch)
                {
                    break;
                }
            }

            if (isMatch)
            {
                matched.Add(row);
            }
        }

        return matched;
    }

    /// <inheritdoc/>
    public override int Rows =>
        _matchedRowIndices is not null ? _matchedRowIndices.Count : Source.Rows;

    /// <inheritdoc/>
    public override object this[int row, int col]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            var sourceRow = _matchedRowIndices is not null ? _matchedRowIndices[row] : row;

            if (col == 0)
            {
                return Source[sourceRow, 0]; // "#" passthrough, never transformed
            }

            var fillValue = FillValues[col];
            if (fillValue is not null)
            {
                // Fill values bypass FormatCellValue by design — they are raw display overrides
                return fillValue;
            }

            var sourceCol = SourceColumnIndices[col] + 1; // +1 skips the "#" pseudo column
            var rawValue = Convert.ToString(Source[sourceRow, sourceCol], CultureInfo.InvariantCulture) ?? string.Empty;
            return FormatCellValue(rawValue, ColumnTypes[col], FormatStrings[col]);
        }
    }
}
