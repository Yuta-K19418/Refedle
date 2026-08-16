using System.Diagnostics;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Engine;

/// <summary>
/// Translates an action stack and input column names into a format-agnostic
/// <see cref="BatchOutputSchema"/> for batch processing.
/// Pure and stateless: no I/O, no side effects.
/// </summary>
public static class ActionApplier
{
    /// <summary>
    /// Builds a <see cref="BatchOutputSchema"/> by applying given actions
    /// to the input column names in order.
    /// </summary>
    /// <param name="columnNames">The full, ordered input column names.</param>
    /// <param name="actions">The ordered list of actions from the recipe.</param>
    /// <returns>
    /// A <see cref="Result{BatchOutputSchema}"/> describing which columns to include
    /// (with their output names) and which filter specs to evaluate.
    /// Returns failure if any action is invalid.
    /// </returns>
    public static Result<BatchOutputSchema> BuildOutputSchema(
        IReadOnlyList<string> columnNames,
        IReadOnlyList<MorphAction> actions
    )
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(actions);

        // Build working columns copy for tracking state changes
        var workingColumns = columnNames
            .Select((name, index) => (Name: name, ColumnIndex: index, OutputName: name))
            .ToList();
        var nameToWorkingIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < workingColumns.Count; i++)
        {
            nameToWorkingIndex[workingColumns[i].Name] = i;
        }

        List<BatchFilterSpec> filterSpecs = [];
        Dictionary<int, CellTransformSpec> transformsByWorkingIndex = [];

        foreach (var action in actions)
        {
            var result = ApplyAction(action, workingColumns, nameToWorkingIndex, filterSpecs, transformsByWorkingIndex);
            if (result.IsFailure)
            {
                return Results.Failure<BatchOutputSchema>(result.Error);
            }
        }

        var outputColumns = BuildOutputColumns(workingColumns, nameToWorkingIndex, transformsByWorkingIndex);
        return Results.Success(new BatchOutputSchema(outputColumns, filterSpecs));
    }

    private static Result ApplyAction(
        MorphAction action,
        List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
        Dictionary<string, int> nameToWorkingIndex,
        List<BatchFilterSpec> filterSpecs,
        Dictionary<int, CellTransformSpec> transformsByWorkingIndex
    ) =>
        action switch
        {
            RenameColumnAction rename => ApplyRename(rename, workingColumns, nameToWorkingIndex),
            DeleteColumnAction delete => ApplyDelete(delete, nameToWorkingIndex),
            CastColumnAction => Results.Success(), // no-op: ColumnType is not tracked
            FilterAction filter => ApplyFilter(filter, workingColumns, nameToWorkingIndex, filterSpecs),
            FillColumnAction fill => ApplyFill(fill, nameToWorkingIndex, transformsByWorkingIndex),
            FormatTimestampAction formatTimestamp
                => ApplyFormatTimestamp(formatTimestamp, nameToWorkingIndex, transformsByWorkingIndex),
            _ => throw new UnreachableException($"Unhandled action type: {action.GetType().Name}"),
        };

    private static Result ApplyRename(
        RenameColumnAction rename,
        List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
        Dictionary<string, int> nameToWorkingIndex
    )
    {
        if (!nameToWorkingIndex.TryGetValue(rename.OldName, out var idx))
        {
            return Results.Success();
        }

        var (name, columnIndex, _) = workingColumns[idx];
        workingColumns[idx] = (name, columnIndex, rename.NewName);
        nameToWorkingIndex.Remove(rename.OldName);
        nameToWorkingIndex[rename.NewName] = idx;
        return Results.Success();
    }

    private static Result ApplyDelete(DeleteColumnAction delete, Dictionary<string, int> nameToWorkingIndex)
    {
        nameToWorkingIndex.Remove(delete.ColumnName);
        return Results.Success();
    }

    private static Result ApplyFilter(
        FilterAction filter,
        List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
        Dictionary<string, int> nameToWorkingIndex,
        List<BatchFilterSpec> filterSpecs
    )
    {
        if (!nameToWorkingIndex.TryGetValue(filter.ColumnName, out var idx))
        {
            return Results.Success();
        }

        var (_, columnIndex, _) = workingColumns[idx];
        filterSpecs.Add(
            new BatchFilterSpec(
                SourceColumnIndex: columnIndex,
                ComparisonType: filter.ComparisonType,
                Operator: filter.Operator,
                Value: filter.Value
            )
        );
        return Results.Success();
    }

    private static Result ApplyFill(
        FillColumnAction fill,
        Dictionary<string, int> nameToWorkingIndex,
        Dictionary<int, CellTransformSpec> transformsByWorkingIndex
    )
    {
        if (!nameToWorkingIndex.TryGetValue(fill.ColumnName, out var idx))
        {
            return Results.Success();
        }

        transformsByWorkingIndex[idx] = new FillSpec(fill.Value);
        return Results.Success();
    }

    private static Result ApplyFormatTimestamp(
        FormatTimestampAction formatTimestamp,
        Dictionary<string, int> nameToWorkingIndex,
        Dictionary<int, CellTransformSpec> transformsByWorkingIndex
    )
    {
        if (!nameToWorkingIndex.TryGetValue(formatTimestamp.ColumnName, out var idx))
        {
            return Results.Success();
        }

        transformsByWorkingIndex[idx] = new TimestampFormatSpec(formatTimestamp.TargetFormat);
        return Results.Success();
    }

    // Filters out deleted columns and preserves working-column order.
    private static List<BatchOutputColumn> BuildOutputColumns(
        List<(string Name, int ColumnIndex, string OutputName)> workingColumns,
        Dictionary<string, int> nameToWorkingIndex,
        Dictionary<int, CellTransformSpec> transformsByWorkingIndex
    )
    {
        List<BatchOutputColumn> outputColumns = [];
        foreach (var kvp in nameToWorkingIndex.OrderBy(kvp => kvp.Value))
        {
            var (name, _, outputName) = workingColumns[kvp.Value];
            var transform = transformsByWorkingIndex.GetValueOrDefault(kvp.Value);
            outputColumns.Add(new BatchOutputColumn(SourceName: name, OutputName: outputName, Transform: transform));
        }

        return outputColumns;
    }
}
