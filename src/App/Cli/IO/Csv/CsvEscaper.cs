
using System.Text;

namespace Refedle.App.Cli.IO.Csv;

internal static class CsvEscaper
{
    internal static string EscapeCsvValue(ReadOnlySpan<char> value)
    {
        bool needsQuoting =
            value.IsEmpty
            || value.Contains('"')
            || value.Contains(',')
            || value.Contains('\n')
            || value.Contains('\r');

        if (!needsQuoting)
        {
            return value.ToString();
        }

        StringBuilder sb = new(value.Length + 2);
        sb.Append('"');
        for (int i = 0; i < value.Length; i++)
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
        bool needsQuoting =
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
        for (int i = 0; i < value.Length; i++)
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
