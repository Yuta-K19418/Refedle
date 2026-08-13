using System.Collections.Frozen;
using System.Globalization;

namespace Refedle.Engine.Models.Actions;

/// <summary>
/// A row-level filter action that retains only source rows satisfying a column value condition.
/// Multiple <see cref="FilterAction"/>s in the action stack are applied with AND semantics.
/// </summary>
public sealed record FilterAction : MorphAction
{
    /// <summary>The name of the column to filter on.</summary>
    public string ColumnName { get; }

    /// <summary>The comparison operator.</summary>
    public FilterOperator Operator { get; }

    /// <summary>How <see cref="Value"/> should be interpreted when evaluating this filter.</summary>
    public ComparisonType ComparisonType { get; }

    /// <summary>The value to compare against (raw string).</summary>
    public string Value { get; }

    private FilterAction(string columnName, FilterOperator op, ComparisonType comparisonType, string value)
    {
        ColumnName = columnName;
        Operator = op;
        ComparisonType = comparisonType;
        Value = value;
    }

    /// <summary>
    /// Validates an operator/comparison-type/value combination without constructing an action.
    /// The same rules are enforced by <see cref="Create"/>, so callers that only need the verdict
    /// (e.g. a dialog at confirm time) can call this directly.
    /// </summary>
    public static Result Validate(FilterOperator op, ComparisonType comparisonType, string value)
    {
        if (!Enum.IsDefined<ComparisonType>(comparisonType))
        {
            return Results.Failure($"'{comparisonType}' is not a defined comparison type.");
        }

        if (!IsValidCombination(op, comparisonType))
        {
            return Results.Failure(
                $"Operator '{op}' is not valid for comparison type '{comparisonType}'.");
        }

        if (!IsValidValue(comparisonType, value))
        {
            return Results.Failure(
                $"Value '{value}' is not parseable as comparison type '{comparisonType}'.");
        }

        return Results.Success();
    }

    /// <summary>
    /// Creates a <see cref="FilterAction"/> after validating the operator/comparison-type/value
    /// combination. Returns a failure result carrying <see cref="Validate"/>'s error otherwise.
    /// </summary>
    public static Result<FilterAction> Create(
        string columnName, FilterOperator op, ComparisonType comparisonType, string value)
    {
        var result = Validate(op, comparisonType, value);
        if (result.IsFailure)
        {
            return Results.Failure<FilterAction>(result.Error);
        }

        return Results.Success(new FilterAction(columnName, op, comparisonType, value));
    }

    private static readonly FrozenSet<FilterOperator> _textOnlyOperators =
        FrozenSet.ToFrozenSet<FilterOperator>(
        [
            FilterOperator.Contains,
            FilterOperator.NotContains,
            FilterOperator.StartsWith,
            FilterOperator.EndsWith,
        ]);

    private static readonly FrozenSet<FilterOperator> _orderingOperators =
        FrozenSet.ToFrozenSet<FilterOperator>(
        [
            FilterOperator.GreaterThan,
            FilterOperator.LessThan,
            FilterOperator.GreaterThanOrEqual,
            FilterOperator.LessThanOrEqual,
        ]);

    private static bool IsValidCombination(FilterOperator op, ComparisonType comparisonType)
    {
        // Equals/NotEquals are valid for every comparison type; the rest are type-specific.
        if (op is FilterOperator.Equals or FilterOperator.NotEquals)
        {
            return true;
        }

        if (comparisonType == ComparisonType.Text)
        {
            return _textOnlyOperators.Contains(op);
        }

        return _orderingOperators.Contains(op);
    }

    private static bool IsValidValue(ComparisonType comparisonType, string value)
        => comparisonType switch
        {
            ComparisonType.Text => true,
            // TryParse accepts NaN/Infinity; comparisons against them are never meaningful.
            ComparisonType.Number => double.TryParse(
                value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed),
            ComparisonType.Timestamp => DateTime.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false,
        };

    /// <inheritdoc/>
    public override string Description => $"Filter '{ColumnName}' {Operator} '{Value}'";
}
