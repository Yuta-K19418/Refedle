using System.Globalization;
using Refedle.Engine.Filtering;
using Refedle.Engine.Types;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// Base class for lazy table transformers that wrap an <see cref="IExtendedTableSource"/> and
/// apply an ordered Action Stack lazily — only to the cells currently requested by the
/// TableView. Owns the transformed per-column state (names, types, source index mapping,
/// fill values, format strings, cast markers) and the shared display-value resolution.
/// Schema derivation lives in <see cref="TransformedSchemaBuilder"/>; cell formatting in
/// <see cref="CellDisplayFormatter"/>. Derived factories must pass the builder's results
/// to the primary constructor and decide how rows resolve against the underlying source
/// (an <see cref="IFilterRowIndexer"/> for streaming sources, synchronous resolution for
/// fully materialized ones).
/// </summary>
internal abstract class LazyTransformerBase(
    IExtendedTableSource source,
    string[] columnNames,
    string[] rawColumnNames,
    IReadOnlyList<ColumnType> columnTypes,
    IReadOnlyList<int> sourceColumnIndices,
    IReadOnlyList<string?> fillValues,
    IReadOnlyList<string?> formatStrings,
    IReadOnlyList<bool> castMarkers
) : IExtendedTableSource, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the wrapped data source providing raw cell values.
    /// </summary>
    protected IExtendedTableSource Source { get; } = source;

    /// <inheritdoc/>
    public string[] ColumnNames { get; } = columnNames;

    /// <summary>
    /// Gets the raw (unlabeled) column names in output order.
    /// Use these when constructing <see cref="Engine.Models.Actions.IMorphAction"/>s so that action
    /// <c>ColumnName</c> values match the schema names used inside <see cref="TransformedSchemaBuilder"/>.
    /// </summary>
    public string[] RawColumnNames { get; } = rawColumnNames;

    /// <inheritdoc/>
    public int Columns => ColumnNames.Length;

    /// <summary>
    /// Gets the output column types after the action stack has been applied.
    /// </summary>
    protected IReadOnlyList<ColumnType> ColumnTypes { get; } = columnTypes;

    /// <summary>
    /// Gets a mapping from output column index to source column index.
    /// </summary>
    protected IReadOnlyList<int> SourceColumnIndices { get; } = sourceColumnIndices;

    /// <summary>
    /// Gets the fill value per output column, or <see langword="null"/> where no
    /// <see cref="Engine.Models.Actions.FillColumnAction"/> targeted the column.
    /// </summary>
    protected IReadOnlyList<string?> FillValues { get; } = fillValues;

    /// <summary>
    /// Gets the timestamp format string per output column, or <see langword="null"/>
    /// where no <see cref="Engine.Models.Actions.FormatTimestampAction"/> targeted the column
    /// and no cast seeded the default format.
    /// </summary>
    protected IReadOnlyList<string?> FormatStrings { get; } = formatStrings;

    /// <summary>
    /// Gets a value per output column indicating whether a
    /// <see cref="Engine.Models.Actions.CastColumnAction"/> targeted it.
    /// Only cast columns and columns with a format string are formatted.
    /// </summary>
    protected IReadOnlyList<bool> CastMarkers { get; } = castMarkers;

    /// <summary>
    /// Gets whether this transformer has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Gets whether the output column at <paramref name="col"/> should be formatted:
    /// a <see cref="Engine.Models.Actions.CastColumnAction"/> targeted it, or it carries a
    /// format string from a <see cref="Engine.Models.Actions.FormatTimestampAction"/>.
    /// Columns hit by no action display their raw value, matching the zero-action
    /// display of the underlying sources.
    /// </summary>
    protected bool ShouldFormat(int col) => CastMarkers[col] || FormatStrings[col] is not null;

    /// <inheritdoc/>
    public abstract long TotalRows { get; }

    /// <inheritdoc/>
    public abstract object this[long row, int col] { get; }

    /// <summary>
    /// Resolves the display value for the output column at <paramref name="col"/> from the
    /// wrapped <see cref="Source"/>. A fill value overrides everything (raw display override
    /// by design); otherwise the raw value is formatted only when <see cref="ShouldFormat"/>
    /// holds, so columns hit by no action display their raw value.
    /// </summary>
    /// <param name="col">Output column index.</param>
    /// <param name="sourceRow">Row index into <see cref="Source"/>.</param>
    /// <param name="sourceCol">Column index into <see cref="Source"/> for this output column.</param>
    protected string ResolveDisplayValue(int col, long sourceRow, int sourceCol)
    {
        var fillValue = FillValues[col];
        if (fillValue is not null)
        {
            return fillValue;
        }

        var rawValue = Convert.ToString(Source[sourceRow, sourceCol], CultureInfo.InvariantCulture) ?? string.Empty;
        return ShouldFormat(col)
            ? CellDisplayFormatter.FormatCellValue(rawValue, ColumnTypes[col], FormatStrings[col])
            : rawValue;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the wrapped <see cref="Source"/> when it is disposable.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> when called from <see cref="Dispose()"/>;
    /// <see langword="false"/> when called from a finalizer (unused — this class has no finalizer).
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing && Source is IDisposable d)
        {
            d.Dispose();
        }

        _disposed = true;
    }
}
