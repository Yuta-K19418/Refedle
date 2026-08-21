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
/// Base class for lazy table transformers that wrap an <see cref="ITableSource"/> and
/// apply an ordered Action Stack of <see cref="MorphAction"/>s lazily — only to the
/// cells currently requested by the TableView.
/// Owns the transformed column schema and shared cell formatting; derived classes
/// decide how rows and columns resolve against the underlying source (an
/// <see cref="IFilterRowIndexer"/> for streaming sources, synchronous resolution for
/// fully materialized ones).
/// Derived factories must compute the transformed schema via
/// <see cref="BuildTransformedSchema"/> and pass the results to the primary constructor.
/// </summary>
internal abstract class LazyTransformerBase(
    ITableSource source,
    string[] columnNames,
    string[] rawColumnNames,
    IReadOnlyList<ColumnType> columnTypes,
    IReadOnlyList<int> sourceColumnIndices,
    IReadOnlyList<string?> fillValues,
    IReadOnlyList<string?> formatStrings
) : ITableSource, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the wrapped data source providing raw cell values.
    /// </summary>
    protected ITableSource Source { get; } = source;

    /// <inheritdoc/>
    public string[] ColumnNames { get; } = columnNames;

    /// <summary>
    /// Gets the raw (unlabeled) column names in output order.
    /// Use these when constructing <see cref="MorphAction"/>s so that action
    /// <c>ColumnName</c> values match the schema names used inside <see cref="BuildTransformedSchema"/>.
    /// </summary>
    internal string[] RawColumnNames { get; } = rawColumnNames;

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
    /// <see cref="FillColumnAction"/> targeted the column.
    /// </summary>
    protected IReadOnlyList<string?> FillValues { get; } = fillValues;

    /// <summary>
    /// Gets the timestamp format string per output column, or <see langword="null"/>
    /// where no <see cref="FormatTimestampAction"/> targeted the column.
    /// </summary>
    protected IReadOnlyList<string?> FormatStrings { get; } = formatStrings;

    /// <summary>
    /// Gets whether this transformer has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public abstract int Rows { get; }

    /// <inheritdoc/>
    public abstract object this[int row, int col] { get; }

    /// <summary>
    /// Applies the action stack sequentially to build the output column names, types,
    /// a mapping array from output column index to source column index, a list of
    /// resolved <see cref="FilterSpec"/>s for any <see cref="FilterAction"/>s encountered,
    /// and a list of format strings for any <see cref="FormatTimestampAction"/>s encountered.
    /// A <see cref="Dictionary{TKey,TValue}"/> keyed by column name provides O(1) lookups per action.
    /// Actions targeting a non-existent column name are silently skipped.
    /// </summary>
    protected static (
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

    protected static void ApplyAction(
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

    protected static void ApplyRename(RenameColumnAction rename, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(rename.OldName, out var renameIdx))
        {
            return;
        }

        working[renameIdx] = working[renameIdx] with { Name = rename.NewName };
        nameToIndex.Remove(rename.OldName);
        nameToIndex[rename.NewName] = renameIdx;
    }

    protected static void ApplyCast(CastColumnAction cast, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(cast.ColumnName, out var castIdx))
        {
            return;
        }

        working[castIdx] = working[castIdx] with { Type = cast.TargetType };
    }

    protected static void ApplyFilter(
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

    protected static void ApplyFill(FillColumnAction fill, List<WorkingColumn> working, Dictionary<string, int> nameToIndex)
    {
        if (!nameToIndex.TryGetValue(fill.ColumnName, out var fillIdx))
        {
            return;
        }

        var inferredType = TypeInferrer.InferType(fill.Value.AsSpan());
        working[fillIdx] = working[fillIdx] with { FillValue = fill.Value, Type = inferredType };
    }

    protected static void ApplyFormatTimestamp(
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
    protected static string FormatCellValue(string rawValue, ColumnType targetType, string? formatString) => targetType switch
    {
        ColumnType.WholeNumber => FormatWholeNumber(rawValue),
        ColumnType.FloatingPoint => FormatFloatingPoint(rawValue),
        ColumnType.Boolean => FormatBoolean(rawValue),
        ColumnType.Timestamp => FormatTimestamp(rawValue, formatString),
        _ => rawValue,
    };

    protected static string FormatWholeNumber(string rawValue) =>
        long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
            ? l.ToString(CultureInfo.InvariantCulture)
            : ParseFailureLabel;

    protected static string FormatFloatingPoint(string rawValue) =>
        double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture)
            : ParseFailureLabel;

    protected static string FormatBoolean(string rawValue)
    {
        if (!bool.TryParse(rawValue, out var b))
        {
            return ParseFailureLabel;
        }

        return b ? "true" : "false";
    }

    protected static string FormatTimestamp(string rawValue, string? formatString)
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
    protected sealed record WorkingColumn(
        int SourceIndex,
        string Name,
        ColumnType Type,
        string? FillValue = null,
        string? FormatString = null
    );

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
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
        if (disposing && Source is IDisposable d)
        {
            d.Dispose();
        }
    }
}
