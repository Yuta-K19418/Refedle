using System.Globalization;
using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Reads only the header row of a CSV file to determine column names, without scanning
/// any data rows or inferring types.
/// </summary>
public static class ColumnNameScanner
{
    /// <summary>
    /// Reads the header row and returns the resolved column names in order. Blank header
    /// cells are auto-named <c>ColumnN</c>. Exact duplicate header names are rejected by the
    /// underlying Sep reader; this method's own uniqueness check covers the remaining case,
    /// where an auto-named blank cell collides with an explicit header name.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <returns>The ordered column names.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown by the underlying Sep reader (at open time) when the header contains exact
    /// duplicate column names.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an auto-named blank cell (<c>ColumnN</c>) collides with another resolved
    /// column name.
    /// </exception>
    public static IReadOnlyList<string> ScanColumnNames(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using SepReader reader = CsvSep.FromFile(filePath);
        SepReaderHeader header = reader.Header;

        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] names = new string[header.ColNames.Count];
        for (int i = 0; i < header.ColNames.Count; i++)
        {
            string name = header.ColNames[i];
            string resolvedName = string.IsNullOrWhiteSpace(name) ? string.Create(CultureInfo.InvariantCulture, $"Column{i + 1}") : name;

            if (!seen.Add(resolvedName))
            {
                throw new InvalidOperationException($"Duplicate column name found: '{resolvedName}'");
            }

            names[i] = resolvedName;
        }

        return names;
    }
}
