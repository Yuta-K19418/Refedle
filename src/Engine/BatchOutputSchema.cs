using Refedle.Engine.Filtering;
using Refedle.Engine.Models;

namespace Refedle.Engine;

/// <summary>
/// Represents a single output column, mapping a source column name to its (possibly renamed)
/// output name.
/// </summary>
/// <param name="SourceName">The original column name in the input file.</param>
/// <param name="OutputName">The column name to use in the output file.</param>
/// <param name="Transform">Optional cell-level transformation; <see langword="null"/> means pass-through.</param>
public sealed record BatchOutputColumn(string SourceName, string OutputName, CellTransformSpec? Transform = null);

/// <summary>
/// Immutable, format-agnostic output plan produced by <see cref="ActionApplier"/>.
/// Describes which columns to project and which filter conditions to evaluate per row.
/// </summary>
/// <param name="Columns">
/// The ordered list of columns to include in the output.
/// Each entry carries the source column name and the (possibly renamed) output name.
/// </param>
/// <param name="Filters">
/// The resolved filter specifications to evaluate per row.
/// Rows that fail any spec are excluded (AND semantics).
/// </param>
public sealed record BatchOutputSchema(
    IReadOnlyList<BatchOutputColumn> Columns,
    IReadOnlyList<BatchFilterSpec> Filters);
