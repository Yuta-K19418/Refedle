
using System.Text;

namespace Refedle.App.Cli.IO.Csv;

internal static class CsvEscaper
{
    internal static string EscapeCsvValue(ReadOnlySpan<char> value)
    {
        var needsQuoting =
            value.IsEmpty
            || value.Contains('"')
            || value.Contains(',')
            || value.Contains('\n')
            || value.Contains('\r');

        if (!needsQuoting)
        {
            return value.ToString();
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                sb.Append("\"\"");
                continue;
            }

            sb.Append(value[i]);
        }

        sb.Append('"');
        return sb.ToString();
    }

    internal static void EscapeCsvValueToBuilder(ReadOnlySpan<char> value, StringBuilder sb)
    {
        var needsQuoting =
            value.IsEmpty
            || value.Contains('"')
            || value.Contains(',')
            || value.Contains('\n')
            || value.Contains('\r');

        if (!needsQuoting)
        {
            sb.Append(value);
            return;
        }

        sb.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                sb.Append("\"\"");
                continue;
            }

            sb.Append(value[i]);
        }

        sb.Append('"');
    }
}
