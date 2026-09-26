using System.Globalization;
using System.Text.Json;
using Refedle.App.Tui.Ui.Views.JsonTreeNodes;

namespace Refedle.App.Tui.Ui.Views;

internal static class Utf8JsonReaderExtensions
{
    internal static string GetPrimitiveDisplay(this ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String =>
                $"\"{JsonTreeNodeHelper.EscapeString(reader.GetString() ?? string.Empty)}\"",
            JsonTokenType.Number when reader.TryGetDecimal(out var d) =>
                d.ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => "<null>",
            _ => "<unknown>",
        };
    }
}
