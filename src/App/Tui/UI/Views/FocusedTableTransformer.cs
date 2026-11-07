using System.Globalization;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// <see cref="LazyTransformerBase"/> subclass for DrillDown results backed by a fully
/// materialized <see cref="FocusedTableSource"/>. Unlike <see cref="LazyTransformer"/>,
/// it resolves <see cref="FilterAction"/>s with a <see cref="FocusedTableFilterRowIndexer"/>
/// that scans the in-memory rows; the caller must run its
/// <see cref="IFilterRowIndexer.BuildIndexAsync"/> on a background task.
/// Column 0 is the <c>"#"</c> pseudo column (a per-row hash), which is never a Morph
/// target: <see cref="Create"/> runs <see cref="LazyTransformerBase.BuildTransformedSchema"/>
/// over the DrillDown schema only, then prepends a fixed <c>"#"</c> slot to every output
/// array. Use <see cref="Create"/> to construct — the constructor is private.
/// </summary>
internal sealed class FocusedTableTransformer : LazyTransformerBase
{
    private readonly IFilterRowIndexer? _filterRowIndexer;

    private FocusedTableTransformer(
        IExtendedTableSource source,
        IFilterRowIndexer? filterRowIndexer,
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings)
        : base(
            source,
            columnNames,
            rawColumnNames,
            columnTypes,
            sourceColumnIndices,
            fillValues,
            formatStrings)
    {
        _filterRowIndexer = filterRowIndexer;
    }

    /// <summary>
    /// Creates a new <see cref="FocusedTableTransformer"/> by applying the action stack
    /// to derive the output column mapping. Filters are resolved by <see cref="FilterRowIndexer"/>.
    /// </summary>
    /// <param name="source">The <see cref="FocusedTableSource"/>-shaped source whose column 0 is the <c>"#"</c> hash column.</param>
    /// <param name="originalSchema">The DrillDown schema before any actions are applied (no <c>"#"</c> entry).</param>
    /// <param name="actions">The ordered list of transformation actions to apply.</param>
    /// <param name="filterRevealThreshold">
    /// Matched rows the filter indexer must confirm before its threshold wait completes.
    /// </param>
    public static FocusedTableTransformer Create(
        IExtendedTableSource source,
        TableSchema originalSchema,
        IReadOnlyList<IMorphAction> actions,
        int filterRevealThreshold
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

        IFilterRowIndexer? filterRowIndexer = schema.filterSpecs.Count > 0
            ? new FocusedTableFilterRowIndexer(source, schema.filterSpecs, filterRevealThreshold)
            : null;

        return new FocusedTableTransformer(
            source,
            filterRowIndexer,
            columnNames,
            rawColumnNames,
            columnTypes,
            sourceColumnIndices,
            fillValues,
            formatStrings
        );
    }

    /// <summary>
    /// Gets the filter row indexer, if any filter is active.
    /// The caller must invoke <see cref="IFilterRowIndexer.BuildIndexAsync"/> on a background task.
    /// </summary>
    internal IFilterRowIndexer? FilterRowIndexer => _filterRowIndexer;

    /// <inheritdoc/>
    public override long TotalRows =>
        _filterRowIndexer is not null ? _filterRowIndexer.TotalMatchedRows : Source.TotalRows;

    /// <inheritdoc/>
    public override object this[long row, int col]
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (row < 0 || row >= TotalRows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            // The filtered row space is indexed by IFilterRowIndexer, which is int-based.
            var sourceRow = _filterRowIndexer is not null
                ? _filterRowIndexer.GetSourceRow((int)row)
                : row;

            if (sourceRow < 0)
            {
                return string.Empty;
            }

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
