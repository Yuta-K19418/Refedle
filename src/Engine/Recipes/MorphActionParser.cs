using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Engine.Recipes;

/// <summary>
/// Constructs <see cref="MorphAction"/> instances from parsed field dictionaries.
/// Field values are expected to be already unquoted.
/// </summary>
internal static class MorphActionParser
{
    /// <summary>
    /// Parses a field dictionary into a <see cref="MorphAction"/>.
    /// Returns a failure result if required fields are missing or invalid.
    /// </summary>
    internal static Result<MorphAction> ParseAction(Dictionary<string, string> fields)
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
                _ => Results.Failure<MorphAction>($"Unknown action type: '{type}'"),
            }
            : Results.Failure<MorphAction>("Missing action type");
    }

    private static Result<MorphAction> ParseRenameAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("oldName", out var oldName))
        {
            return Results.Failure<MorphAction>("Missing required field 'oldName' for rename action");
        }

        if (!fields.TryGetValue("newName", out var newName))
        {
            return Results.Failure<MorphAction>("Missing required field 'newName' for rename action");
        }

        return Results.Success<MorphAction>(new RenameColumnAction
        {
            OldName = oldName,
            NewName = newName,
        });
    }

    private static Result<MorphAction> ParseDeleteAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("columnName", out var columnName))
        {
            return Results.Failure<MorphAction>("Missing required field 'columnName' for delete action");
        }

        return Results.Success<MorphAction>(new DeleteColumnAction { ColumnName = columnName });
    }

    private static Result<MorphAction> ParseCastAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("columnName", out var columnName))
        {
            return Results.Failure<MorphAction>("Missing required field 'columnName' for cast action");
        }

        if (!fields.TryGetValue("targetType", out var targetTypeStr))
        {
            return Results.Failure<MorphAction>("Missing required field 'targetType' for cast action");
        }

        if (!Enum.TryParse<ColumnType>(targetTypeStr, ignoreCase: false, out var targetType))
        {
            return Results.Failure<MorphAction>($"Invalid enum value for targetType: '{targetTypeStr}'");
        }

        return Results.Success<MorphAction>(new CastColumnAction
        {
            ColumnName = columnName,
            TargetType = targetType,
        });
    }

    private static Result<MorphAction> ParseFillAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("columnName", out var columnName))
        {
            return Results.Failure<MorphAction>("Missing required field 'columnName' for fill action");
        }

        if (!fields.TryGetValue("value", out var value))
        {
            return Results.Failure<MorphAction>("Missing required field 'value' for fill action");
        }

        return Results.Success<MorphAction>(new FillColumnAction { ColumnName = columnName, Value = value });
    }

    private static Result<MorphAction> ParseFilterAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("columnName", out var columnName))
        {
            return Results.Failure<MorphAction>("Missing required field 'columnName' for filter action");
        }

        if (!fields.TryGetValue("operator", out var operatorStr))
        {
            return Results.Failure<MorphAction>("Missing required field 'operator' for filter action");
        }

        if (!fields.TryGetValue("value", out var filterValue))
        {
            return Results.Failure<MorphAction>("Missing required field 'value' for filter action");
        }

        if (!Enum.TryParse<FilterOperator>(operatorStr, ignoreCase: false, out var filterOperator))
        {
            return Results.Failure<MorphAction>($"Invalid enum value for operator: '{operatorStr}'");
        }

        if (!fields.TryGetValue("comparisonType", out var comparisonTypeStr))
        {
            return Results.Failure<MorphAction>("Missing required field 'comparisonType' for filter action");
        }

        if (!Enum.TryParse<ComparisonType>(comparisonTypeStr, ignoreCase: false, out var comparisonType))
        {
            return Results.Failure<MorphAction>($"Invalid enum value for comparisonType: '{comparisonTypeStr}'");
        }

        var action = FilterAction.Create(columnName, filterOperator, comparisonType, filterValue);
        if (action.IsFailure)
        {
            return Results.Failure<MorphAction>(action.Error);
        }

        return Results.Success<MorphAction>(action.Value);
    }

    private static Result<MorphAction> ParseFormatTimestampAction(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("columnName", out var columnName))
        {
            return Results.Failure<MorphAction>("Missing required field 'columnName' for format_timestamp action");
        }

        if (!fields.TryGetValue("targetFormat", out var targetFormat))
        {
            return Results.Failure<MorphAction>("Missing required field 'targetFormat' for format_timestamp action");
        }

        if (string.IsNullOrWhiteSpace(targetFormat))
        {
            return Results.Failure<MorphAction>("Field 'targetFormat' must not be empty for format_timestamp action");
        }

        return Results.Success<MorphAction>(new FormatTimestampAction
        {
            ColumnName = columnName,
            TargetFormat = targetFormat,
        });
    }
}
