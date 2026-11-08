using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Constructs <see cref="IMorphAction"/> instances from parsed field dictionaries.
/// Field values are expected to be already unquoted.
/// </summary>
internal static class MorphActionParser
{
    private const string ColumnNameKey = "columnName";

    /// <summary>
    /// Parses a field dictionary into a <see cref="IMorphAction"/>.
    /// Returns a failure result if required fields are missing or invalid.
    /// </summary>
    internal static Result<IMorphAction> ParseAction(Dictionary<string, string> fields)
    {
        return fields.TryGetValue("type", out var type)
            ? type switch
            {
                "Rename" => ParseRenameAction(fields),
                "Delete" => ParseDeleteAction(fields),
                "Cast" => ParseCastAction(fields),
                "Filter" => ParseFilterAction(fields),
                "Fill" => ParseFillAction(fields),
                "FormatTimestamp" => ParseFormatTimestampAction(fields),
                _ => Results.Failure<IMorphAction>($"Unknown action type: '{type}'"),
            }
            : Results.Failure<IMorphAction>("Missing action type");
    }

    private static Result<IMorphAction> ParseRenameAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("oldName", out var oldName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'oldName' for rename action");
        }

        // Kept as if: a preceding guard also returns; folding both would chain conditionals.
#pragma warning disable IDE0046
        if (!fields.TryGetValue("newName", out var newName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'newName' for rename action");
        }
#pragma warning restore IDE0046

        return Results.Success<IMorphAction>(new RenameColumnAction
        {
            OldName = oldName,
            NewName = newName,
        });
    }

    private static Result<IMorphAction> ParseDeleteAction(Dictionary<string, string> fields)
    {
        return !fields.TryGetValue(ColumnNameKey, out var columnName)
            ? Results.Failure<IMorphAction>("Missing required field 'columnName' for delete action")
            : Results.Success<IMorphAction>(new DeleteColumnAction { ColumnName = columnName });
    }

    private static Result<IMorphAction> ParseCastAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue(ColumnNameKey, out var columnName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'columnName' for cast action");
        }

        if (!fields.TryGetValue("targetType", out var targetTypeStr))
        {
            return Results.Failure<IMorphAction>("Missing required field 'targetType' for cast action");
        }

        // Kept as if: a preceding guard also returns; folding both would chain conditionals.
#pragma warning disable IDE0046
        if (!Enum.TryParse<ColumnType>(targetTypeStr, ignoreCase: false, out var targetType))
        {
            return Results.Failure<IMorphAction>($"Invalid enum value for targetType: '{targetTypeStr}'");
        }
#pragma warning restore IDE0046

        return Results.Success<IMorphAction>(new CastColumnAction
        {
            ColumnName = columnName,
            TargetType = targetType,
        });
    }

    private static Result<IMorphAction> ParseFillAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue(ColumnNameKey, out var columnName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'columnName' for fill action");
        }

        // Kept as if: a preceding guard also returns; folding both would chain conditionals.
#pragma warning disable IDE0046
        if (!fields.TryGetValue("value", out var value))
        {
            return Results.Failure<IMorphAction>("Missing required field 'value' for fill action");
        }
#pragma warning restore IDE0046

        return Results.Success<IMorphAction>(new FillColumnAction { ColumnName = columnName, Value = value });
    }

    private static Result<IMorphAction> ParseFilterAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue(ColumnNameKey, out var columnName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'columnName' for filter action");
        }

        if (!fields.TryGetValue("operator", out var operatorStr))
        {
            return Results.Failure<IMorphAction>("Missing required field 'operator' for filter action");
        }

        if (!fields.TryGetValue("value", out var filterValue))
        {
            return Results.Failure<IMorphAction>("Missing required field 'value' for filter action");
        }

        if (!Enum.TryParse<FilterOperator>(operatorStr, ignoreCase: false, out var filterOperator))
        {
            return Results.Failure<IMorphAction>($"Invalid enum value for operator: '{operatorStr}'");
        }

        if (!fields.TryGetValue("comparisonType", out var comparisonTypeStr))
        {
            return Results.Failure<IMorphAction>("Missing required field 'comparisonType' for filter action");
        }

        if (!Enum.TryParse<ComparisonType>(comparisonTypeStr, ignoreCase: false, out var comparisonType))
        {
            return Results.Failure<IMorphAction>($"Invalid enum value for comparisonType: '{comparisonTypeStr}'");
        }

        var action = FilterAction.Create(columnName, filterOperator, comparisonType, filterValue);
        return action.IsFailure
            ? Results.Failure<IMorphAction>(action.Error)
            : Results.Success<IMorphAction>(action.Value);
    }

    private static Result<IMorphAction> ParseFormatTimestampAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue(ColumnNameKey, out var columnName))
        {
            return Results.Failure<IMorphAction>("Missing required field 'columnName' for format_timestamp action");
        }

        if (!fields.TryGetValue("targetFormat", out var targetFormat))
        {
            return Results.Failure<IMorphAction>("Missing required field 'targetFormat' for format_timestamp action");
        }

        // Kept as if: a preceding guard also returns; folding both would chain conditionals.
#pragma warning disable IDE0046
        if (string.IsNullOrWhiteSpace(targetFormat))
        {
            return Results.Failure<IMorphAction>("Field 'targetFormat' must not be empty for format_timestamp action");
        }
#pragma warning restore IDE0046

        return Results.Success<IMorphAction>(new FormatTimestampAction
        {
            ColumnName = columnName,
            TargetFormat = targetFormat,
        });
    }
}
