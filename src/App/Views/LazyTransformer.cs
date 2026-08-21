using System.Globalization;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.App.Views;

/// <summary>
/// Wraps an <see cref="ITableSource"/> and applies an ordered Action Stack of
/// <see cref="MorphAction"/>s lazily — only to the cells currently requested by the TableView.
/// When one or more <see cref="FilterAction"/>s are present, uses an
/// <see cref="IFilterRowIndexer"/> (provided via a factory in <see cref="Create"/>)
/// to map filtered row indices to source rows.
/// Use <see cref="Create"/> to construct — the constructor is private.
/// </summary>
internal sealed class LazyTransformer : LazyTransformerBase
{
    private readonly IFilterRowIndexer? _filterRowIndexer;

    private LazyTransformer(
        ITableSource source,
        IFilterRowIndexer? filterRowIndexer,
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings
    )
        : base(
            source,
            columnNames,
            rawColumnNames,
            columnTypes,
            sourceColumnIndices,
            fillValues,
            formatStrings
        )
    {
        _filterRowIndexer = filterRowIndexer;
    }

    /// <summary>
    /// Creates a new <see cref="LazyTransformer"/> by applying the action stack to derive
    /// the output column mapping on creation.
    /// </summary>
    /// <param name="source">The underlying data source providing raw cell values.</param>
    /// <param name="originalSchema">The schema of the source before any actions are applied.</param>
    /// <param name="actions">The ordered list of transformation actions to apply.</param>
    /// <param name="filterRowIndexerFactory">
    /// Optional factory that receives resolved <see cref="FilterSpec"/>s and returns an
    /// <see cref="IFilterRowIndexer"/>. Pass <see langword="null"/> to disable row filtering.
    /// The caller is responsible for invoking <see cref="IFilterRowIndexer.BuildIndexAsync"/>
    /// on a background task after construction.
    /// </param>
    public static LazyTransformer Create(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions,
        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer>? filterRowIndexerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(originalSchema);
        ArgumentNullException.ThrowIfNull(actions);

        var schema = BuildTransformedSchema(originalSchema, actions);
        var filterRowIndexer = schema.filterSpecs.Count > 0 && filterRowIndexerFactory is not null
            ? filterRowIndexerFactory(schema.filterSpecs)
            : null;

        return new LazyTransformer(
            source,
            filterRowIndexer,
            schema.columnNames,
            schema.rawColumnNames,
            schema.columnTypes,
            schema.sourceColumnIndices,
            schema.fillValues,
            schema.formatStrings
        );
    }

    /// <summary>
    /// Gets the filter row indexer created by the factory, if any.
    /// The caller must invoke <see cref="IFilterRowIndexer.BuildIndexAsync"/> on a background task.
    /// </summary>
    internal IFilterRowIndexer? FilterRowIndexer => _filterRowIndexer;

    /// <inheritdoc/>
    /// <remarks>
    /// When <see cref="FilterRowIndexer"/> is present, the value may be partial
    /// while <see cref="IFilterRowIndexer.BuildIndexAsync"/> is still running
    /// on a background task. The caller is responsible for waiting for index
    /// build completion before displaying row counts.
    /// </remarks>
    public override int Rows =>
        _filterRowIndexer is not null ? _filterRowIndexer.TotalMatchedRows : Source.Rows;

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

            var sourceRow = _filterRowIndexer is not null
                ? _filterRowIndexer.GetSourceRow(row)
                : row;

            if (sourceRow < 0)
            {
                return string.Empty;
            }

            var fillValue = FillValues[col];
            if (fillValue is not null)
            {
                // Fill values bypass FormatCellValue by design — they are raw display overrides
                return fillValue;
            }

            var sourceCol = SourceColumnIndices[col];
            var rawValue = Convert.ToString(Source[sourceRow, sourceCol], CultureInfo.InvariantCulture) ?? string.Empty;
            return FormatCellValue(rawValue, ColumnTypes[col], FormatStrings[col]);
        }
    }
}
