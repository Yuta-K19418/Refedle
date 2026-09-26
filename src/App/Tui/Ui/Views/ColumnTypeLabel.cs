using Refedle.Engine.Types;

namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// Provides display labels for column types in table view headers.
/// </summary>
internal static class ColumnTypeLabel
{
    internal static string ToLabel(ColumnType type) => type switch
    {
        ColumnType.Text => "text",
        ColumnType.WholeNumber => "number",
        ColumnType.FloatingPoint => "float",
        ColumnType.Boolean => "bool",
        ColumnType.Timestamp => "datetime",
        ColumnType.JsonObject => "object",
        ColumnType.JsonArray => "array",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unexpected ColumnType: {type}"),
    };
}
