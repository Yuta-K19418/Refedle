using nietras.SeparatedValues;

namespace Refedle.Engine.IO.Csv;

/// <summary>
/// Reads only the header row of a CSV file to determine column names, without scanning
/// any data rows or inferring types. Used by the CLI batch pipeline, which no longer needs
/// column types (see design_cli_batch_column_resolution.md).
/// </summary>
public static class ColumnNameScanner
{
    /// <summary>
    /// Reads the header row and returns the resolved column names in order. Blank header
    /// cells are auto-named <c>ColumnN</c>; duplicate names (explicit or auto-named) throw.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    /// <returns>The ordered column names.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a duplicate column name is found.</exception>
    public static IReadOnlyList<string> ScanColumnNames(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using var reader = Sep.New(',').Reader().FromFile(filePath);
        var header = reader.Header;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new string[header.ColNames.Count];
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            var name = header.ColNames[i];
            var resolvedName = string.IsNullOrWhiteSpace(name) ? $"Column{i + 1}" : name;

            if (!seen.Add(resolvedName))
            {
                throw new InvalidOperationException($"Duplicate column name found: '{resolvedName}'");
            }

            names[i] = resolvedName;
        }

        return names;
    }
}
