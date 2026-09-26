using System.Text.Json;

namespace Refedle.App.Tui.Ui.Views;

internal static class JsonTokenTypeExtensions
{
    internal static JsonValueKind ToJsonValueKind(this JsonTokenType tokenType)
    {
        return tokenType switch
        {
            JsonTokenType.String => JsonValueKind.String,
            JsonTokenType.Number => JsonValueKind.Number,
            JsonTokenType.True => JsonValueKind.True,
            JsonTokenType.False => JsonValueKind.False,
            JsonTokenType.Null => JsonValueKind.Null,
            _ => JsonValueKind.Undefined,
        };
    }
}
