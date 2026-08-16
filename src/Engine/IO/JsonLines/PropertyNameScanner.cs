using System.Diagnostics;
using System.Text.Json;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Collects JSON object property names across JSON Lines rows, in first-appearance order,
/// without inferring value types.
/// </summary>
public static class PropertyNameScanner
{
    /// <summary>
    /// Scans one batch of raw lines, adding any newly-seen property names to
    /// <paramref name="seen"/>/<paramref name="order"/>. Intended to be called once per batch
    /// by a caller reading a JSON Lines file in bounded-size chunks (see Phase 2), so the same
    /// pair of accumulator collections is shared and grown across repeated calls rather than
    /// each call allocating and returning its own list.
    /// </summary>
    public static void ScanPropertyNames(IReadOnlyList<JsonRawBytes> rawLines, ISet<string> seen, IList<string> order)
    {
        ArgumentNullException.ThrowIfNull(rawLines);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(order);

        // One scratch list per batch, cleared per line, keeps the full-file scan allocation-light
        // while names still merge only after a line parses completely (transactional per line).
        List<string> lineNames = [];
        foreach (var line in rawLines)
        {
            lineNames.Clear();
            ScanLine(line.Span, lineNames, seen, order);
        }
    }

    private static void ScanLine(ReadOnlySpan<byte> line, List<string> lineNames, ISet<string> seen, IList<string> order)
    {
        try
        {
            var reader = new Utf8JsonReader(line);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    MergeNamesIfEndOfLine(ref reader, lineNames, seen, order);
                    return;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ScanProperty(ref reader, lineNames);
            }
        }
        catch (JsonException)
        {
            // Malformed line: skip it, matching SchemaScanner.RefineSchema's fail-soft behavior.
        }
    }

    // A line holds exactly one object: only whitespace may follow its closing brace. Read()
    // skips whitespace, so false means a clean end of line; another token (true) or a
    // JsonException marks invalid trailing content and leaves the line's names unmerged.
    private static void MergeNamesIfEndOfLine(ref Utf8JsonReader reader, List<string> lineNames, ISet<string> seen, IList<string> order)
    {
        if (reader.Read())
        {
            return;
        }

        MergeLineNames(lineNames, seen, order);
    }

    // Reads one "key": value pair (reader positioned at PropertyName); nested object/array
    // values are skipped whole.
    private static void ScanProperty(ref Utf8JsonReader reader, List<string> lineNames)
    {
        var propertyName = reader.GetString() ?? throw new UnreachableException("GetString() returned null on a PropertyName token.");
        lineNames.Add(propertyName);

        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }
    }

    private static void MergeLineNames(List<string> lineNames, ISet<string> seen, IList<string> order)
    {
        foreach (var name in lineNames)
        {
            if (seen.Add(name))
            {
                order.Add(name);
            }
        }
    }
}
