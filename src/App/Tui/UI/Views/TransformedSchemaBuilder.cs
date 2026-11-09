using System.Diagnostics;
using Refedle.Engine.Filtering;
using Refedle.Engine.IO.Csv;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.App.Tui.UI.Views;

/// <summary>
/// Derives the transformed column schema consumed by the lazy table transformers:
/// applies an ordered Action Stack of <see cref="IMorphAction"/>s to a
/// <see cref="TableSchema"/> and produces the output column names, raw names, types,
/// source index mapping, fill values, timestamp format strings, cast markers, and
/// resolved <see cref="FilterSpec"/>s.
/// Actions targeting a non-existent column name are silently skipped.
/// </summary>
internal static class TransformedSchemaBuilder
{
    /// <summary>
    /// Applies the action stack sequentially to build the transformed column schema.
    /// A <see cref="Dictionary{TKey,TValue}"/> keyed by column name provides O(1) lookups per action.
    /// </summary>
    internal static (
        string[] columnNames,
        string[] rawColumnNames,
        IReadOnlyList<ColumnType> columnTypes,
        IReadOnlyList<int> sourceColumnIndices,
        IReadOnlyList<string?> fillValues,
        IReadOnlyList<string?> formatStrings,
        IReadOnlyList<bool> castMarkers,
        IReadOnlyList<FilterSpec> filterSpecs
    ) Build(TableSchema originalSchema, IReadOnlyList<IMorphAction> actions)
    {
        var working = originalSchema
            .Columns.Select(static c => new WorkingColumn(
                SourceIndex: c.ColumnIndex,
                Name: c.Name,
                Type: c.Type
            ))
            .ToList();

        var nameToIndex = working.Select(static (w, i) => (w.Name, i)).ToDictionary(static t => t.Name, static t => t.i, StringComparer.Ordinal);
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
                .ConvertAll(static workingColumn => $"{workingColumn.Name} ({ColumnTypeLabel.ToLabel(workingColumn.Type)})")
                .ToArray(),
            remaining.ConvertAll(static workingColumn => workingColumn.Name).ToArray(),
            remaining.ConvertAll(static workingColumn => workingColumn.Type),
            remaining.ConvertAll(static workingColumn => workingColumn.SourceIndex),
            remaining.ConvertAll(static workingColumn => workingColumn.FillValue),
            remaining.ConvertAll(static workingColumn => workingColumn.FormatString),
            remaining.ConvertAll(static workingColumn => workingColumn.WasCast),
            filterSpecs
        );
    }

    private static void ApplyAction(
        IMorphAction action,
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
                throw new UnreachableException($"Unhandled {nameof(IMorphAction)} type: {action.GetType()}");
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

        var col = working[castIdx];
        string? formatString = col.FormatString;
        if (formatString is null && cast.TargetType == ColumnType.Timestamp)
        {
            // Seed the default so a cast Timestamp column renders formatted; an existing
            // format (set via FormatTimestamp) is never replaced by a later cast.
            formatString = CellDisplayFormatter.DefaultTimestampFormat;
        }

        working[castIdx] = col with { Type = cast.TargetType, WasCast = true, FormatString = formatString };
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

        // An empty target format falls back to the default; an explicit format replaces
        // any earlier one (last write wins, matching the engine's per-column transform map).
        working[fmtIdx] = working[fmtIdx] with
        {
            FormatString = string.IsNullOrEmpty(formatTs.TargetFormat)
                ? CellDisplayFormatter.DefaultTimestampFormat
                : formatTs.TargetFormat,
        };
    }

    /// <summary>
    /// Internal working representation of a column during schema transformation.
    /// Tracks source column index, current name, type, optional fill value, optional
    /// format string, and whether a cast targeted the column.
    /// </summary>
    private sealed record WorkingColumn(
        int SourceIndex,
        string Name,
        ColumnType Type,
        string? FillValue = null,
        string? FormatString = null,
        bool WasCast = false
    );
}
