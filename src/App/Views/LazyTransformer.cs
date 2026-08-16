using System.Diagnostics;
using System.Globalization;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.App.Views;

/// <summary>
/// Wraps an <see cref="ITableSource"/> and applies an ordered Action Stack of
/// <see cref="MorphAction"/>s lazily — only to the cells currently requested by the TableView.
/// Constructs the output column mapping on initialization,
/// then delegates cell access to the underlying source on demand.
/// When one or more <see cref="FilterAction"/>s are present, uses an
/// <see cref="IFilterRowIndexer"/> (provided via a factory in the constructor)
/// to map filtered row indices to source rows.
/// </summary>
internal sealed class LazyTransformer : ITableSource, IDisposable
{
    private readonly ITableSource _source;
    private readonly IReadOnlyList<int> _sourceColumnIndices;
    private readonly string[] _columnNames;
    private readonly string[] _rawColumnNames;
    private readonly IReadOnlyList<ColumnType> _columnTypes;
    private readonly IReadOnlyList<string?> _fillValues;
    private readonly IReadOnlyList<string?> _formatStrings;
    private readonly IFilterRowIndexer? _filterRowIndexer;

    /// <summary>
    /// Initializes a new instance of <see cref="LazyTransformer"/>.
    /// Applies the action stack to derive the output column mapping on construction.
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
    public LazyTransformer(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions,
        Func<IReadOnlyList<FilterSpec>, IFilterRowIndexer>? filterRowIndexerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(originalSchema);
        ArgumentNullException.ThrowIfNull(actions);

        _source = source;
        (_columnNames, _rawColumnNames, _columnTypes, _sourceColumnIndices, _fillValues, _formatStrings, var filterSpecs) =
            BuildTransformedSchema(originalSchema, actions);

        if (filterSpecs.Count > 0 && filterRowIndexerFactory is not null)
        {
            _filterRowIndexer = filterRowIndexerFactory(filterSpecs);
        }
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
    public int Rows =>
        _filterRowIndexer is not null ? _filterRowIndexer.TotalMatchedRows : _source.Rows;

    /// <inheritdoc/>
    public int Columns => _columnNames.Length;

    /// <inheritdoc/>
    public string[] ColumnNames => _columnNames;

    /// <summary>
    /// Gets the raw (unlabeled) column names in output order.
    /// Use these when constructing <see cref="MorphAction"/>s so that action
    /// <c>ColumnName</c> values match the schema names used inside <see cref="BuildTransformedSchema"/>.
    /// </summary>
    internal string[] RawColumnNames => _rawColumnNames;

    private bool _disposed;

    /// <inheritdoc/>
    public object this[int row, int col]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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

            var fillValue = _fillValues[col];
            if (fillValue is not null)
            {
                // Fill values bypass FormatCellValue by design — they are raw display overrides
                return fillValue;
            }

            var sourceCol = _sourceColumnIndices[col];
            var rawValue = Convert.ToString(_source[sourceRow, sourceCol], CultureInfo.InvariantCulture) ?? string.Empty;
            return FormatCellValue(rawValue, _columnTypes[col], _formatStrings[col]);
        }
    }

    /// <summary>
    /// Applies the action stack sequentially to build the output column names, types,
    /// a mapping array from output column index to source column index, a list of
    /// resolved <see cref="FilterSpec"/>s for any <see cref="FilterAction"/>s encountered,
    /// and a list of format strings for any <see cref="FormatTimestampAction"/>s encountered.
    /// A <see cref="Dictionary{TKey,TValue}"/> keyed by column name provides O(1) lookups per action.
    /// Actions targeting a non-existent column name are silently skipped.
    /// </summary>
    private static (
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings,
        IReadOnlyList<FilterSpec> filterSpecs
    ) BuildTransformedSchema(TableSchema originalSchema, IReadOnlyList<MorphAction> actions)
    {
        var working = originalSchema
            .Columns.Select(c => new WorkingColumn(
                SourceIndex: c.ColumnIndex,
                Name: c.Name,
                Type: c.Type
            ))
            .ToList();

        var nameToIndex = working.Select((w, i) => (w.Name, i)).ToDictionary(t => t.Name, t => t.i, StringComparer.Ordinal);
        List<FilterSpec> filterSpecs = [];

        foreach (var action in actions)
        {
            ApplyAction(action, working, nameToIndex, filterSpecs);
        }

        // Pre-size to avoid reallocation; collection expressions do not support capacity hints.
        var remaining = new List<WorkingColumn>(nameToIndex.Count);
        foreach (var idx in nameToIndex.Values.Order())
        {
            remaining.Add(working[idx]);
        }

        return (
            remaining
                .ConvertAll(workingColumn => $"{workingColumn.Name} ({ColumnTypeLabel.ToLabel(workingColumn.Type)})")
                .ToArray(),
            remaining.ConvertAll(workingColumn => workingColumn.Name).ToArray(),
            remaining.ConvertAll(workingColumn => workingColumn.Type),
            remaining.ConvertAll(workingColumn => workingColumn.SourceIndex),
            remaining.ConvertAll(workingColumn => workingColumn.FillValue),
            remaining.ConvertAll(workingColumn => workingColumn.FormatString),
            filterSpecs
        );
    }

    private static void ApplyAction(
        MorphAction action,
        List<WorkingColumn> working,
        Dictionary<string, int> nameToIndex,
        List<FilterSpec> filterSpecs)
    {
        switch (action)
        {
            case RenameColumnAction rename:
                ApplyRename(rename, working, nameToIndex);
                break;
            case DeleteColumnAction delete:
                nameToIndex.Remove(delete.ColumnName);
                break;
            case CastColumnAction cast:
                ApplyCast(cast, working, nameToIndex);
                break;
            case FilterAction filter:
                ApplyFilter(filter, working, nameToIndex, filterSpecs);
                break;
            case FillColumnAction fill:
                ApplyFill(fill, working, nameToIndex);
                break;
            case FormatTimestampAction formatTs:
                ApplyFormatTimestamp(formatTs, working, nameToIndex);
                break;
            default:
                throw new UnreachableException($"Unhandled {nameof(MorphAction)} type: {action.GetType()}");
        }
    }

    private static void ApplyRename(RenameColumnAction rename, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(rename.OldName, out var renameIdx))
        {
            return;
        }

        working[renameIdx] = working[renameIdx] with { Name = rename.NewName };
        nameToIndex.Remove(rename.OldName);
        nameToIndex[rename.NewName] = renameIdx;
    }

    private static void ApplyCast(CastColumnAction cast, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(cast.ColumnName, out var castIdx))
        {
            return;
        }

        working[castIdx] = working[castIdx] with { Type = cast.TargetType };
    }

    private static void ApplyFilter(
        FilterAction filter,
        List<WorkingColumn> working,
        Dictionary<string, int> nameToIndex,
        List<FilterSpec> filterSpecs)
    {
        // Row-level filter: does not modify column schema.
        // Resolve column name to source index and record FilterSpec.
        if (!nameToIndex.TryGetValue(filter.ColumnName, out var filterIdx))
        {
            return;
        }

        var col = working[filterIdx];
        filterSpecs.Add(
            new FilterSpec(
                SourceColumnIndex: col.SourceIndex,
                ColumnType: col.Type,
                Operator: filter.Operator,
                Value: filter.Value
            )
        );
    }

    private static void ApplyFill(FillColumnAction fill, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(fill.ColumnName, out var fillIdx))
        {
            return;
        }

        var inferredType = TypeInferrer.InferType(fill.Value.AsSpan());
        working[fillIdx] = working[fillIdx] with { FillValue = fill.Value, Type = inferredType };
    }

    private static void ApplyFormatTimestamp(
        FormatTimestampAction formatTs,
        List<WorkingColumn> working,
        Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(formatTs.ColumnName, out var fmtIdx))
        {
            return;
        }

        working[fmtIdx] = working[fmtIdx] with { FormatString = formatTs.TargetFormat };
    }

    private const string ParseFailureLabel = "<invalid>";

    /// <summary>
    /// Formats a raw cell string value according to the target column type.
    /// Returns the raw value for <see cref="ColumnType.Text"/>, <see cref="ColumnType.JsonObject"/>,
    /// and <see cref="ColumnType.JsonArray"/>. Returns <c>"&lt;invalid&gt;"</c> if parsing fails.
    /// </summary>
    private static string FormatCellValue(string rawValue, ColumnType targetType, string? formatString) => targetType switch
    {
        ColumnType.WholeNumber => FormatWholeNumber(rawValue),
        ColumnType.FloatingPoint => FormatFloatingPoint(rawValue),
        ColumnType.Boolean => FormatBoolean(rawValue),
        ColumnType.Timestamp => FormatTimestamp(rawValue, formatString),
        _ => rawValue,
    };

    private static string FormatWholeNumber(string rawValue) =>
        long.TryParse(rawValue, out var l) ? l.ToString(CultureInfo.InvariantCulture) : ParseFailureLabel;

    private static string FormatFloatingPoint(string rawValue) =>
        double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture)
            : ParseFailureLabel;

    private static string FormatBoolean(string rawValue)
    {
        if (!bool.TryParse(rawValue, out var b))
        {
            return ParseFailureLabel;
        }

        return b ? "true" : "false";
    }

    private static string FormatTimestamp(string rawValue, string? formatString)
    {
        if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return ParseFailureLabel;
        }

        var format = string.IsNullOrEmpty(formatString) ? "yyyy-MM-dd HH:mm:ss" : formatString;
        return dt.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Internal working representation of a column during schema transformation.
    /// Tracks source column index, current name, type, optional fill value, and optional format string.
    /// </summary>
    private sealed record WorkingColumn(
        int SourceIndex,
        string Name,
        ColumnType Type,
        string? FillValue = null,
        string? FormatString = null
    );

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_source is IDisposable d)
        {
            d.Dispose();
        }

        _disposed = true;
    }
}
