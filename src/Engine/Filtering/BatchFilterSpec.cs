using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Engine.Filtering;

/// <summary>
/// Resolved filter specification used internally by the CLI batch pipeline
/// (the record-reader implementations). Unlike <see cref="FilterSpec"/> (TUI),
/// it carries a <see cref="ComparisonType"/> instead of a pre-resolved
/// <see cref="ColumnType"/>, so the CLI resolves type per row instead of
/// trusting the schema scan.
/// </summary>
public readonly record struct BatchFilterSpec(
    int SourceColumnIndex,
    ComparisonType ComparisonType,
    FilterOperator Operator,
    string Value
);
