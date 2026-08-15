using System.Diagnostics;
using System.Text.Json;

namespace Refedle.Engine.IO.JsonLines;

/// <summary>
/// Collects JSON object property names across JSON Lines rows, in first-appearance order,
/// without inferring value types. Used by the CLI batch pipeline, which no longer needs
/// column types (see design_cli_batch_column_resolution.md).
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
    public static void ScanPropertyNames(IReadOnlyList<JsonRawBytes> rawLines, HashSet<string> seen, IList<string> order)
    {
        ArgumentNullException.ThrowIfNull(rawLines);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(order);

        foreach (var line in rawLines)
        {
            ScanLine(line.Span, seen, order);
        }
    }

    private static void ScanLine(ReadOnlySpan<byte> line, HashSet<string> seen, IList<string> order)
    {
        try
        {
            var reader = new Utf8JsonReader(line);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ScanProperty(ref reader, seen, order);
            }
        }
        catch (JsonException)
        {
            // Malformed line: skip it, matching SchemaScanner.RefineSchema's fail-soft behavior.
        }
    }

    // Reads one "key": value pair (reader positioned at PropertyName) and registers the key
    // in first-seen order; nested object/array values are skipped whole.
    private static void ScanProperty(ref Utf8JsonReader reader, HashSet<string> seen, IList<string> order)
    {
        var propertyName = reader.GetString() ?? throw new UnreachableException("GetString() returned null on a PropertyName token.");
        if (seen.Add(propertyName))
        {
            order.Add(propertyName);
        }

        if (!reader.Read())
        {
            return;
        }

        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }
    }
}
