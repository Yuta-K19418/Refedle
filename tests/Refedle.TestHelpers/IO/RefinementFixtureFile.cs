namespace Refedle.TestHelpers.IO;

/// <summary>
/// Writes the JSON Lines fixture shared by the schema-refinement tests.
/// </summary>
internal static class RefinementFixtureFile
{
    /// <summary>
    /// Writes a JSON Lines fixture whose first 200 lines fit the initial scan (single
    /// "name" column) and whose remaining lines introduce an "age" column, so the
    /// background refinement must produce a schema that differs from the initial one.
    /// </summary>
    public static async Task WriteRefinementFixtureAsync(string path)
    {
        IEnumerable<string> initialLines = Enumerable.Range(0, 200).Select(i => $"{{\"name\":\"user{i}\"}}");
        IEnumerable<string> refinedLines = Enumerable.Range(0, 5).Select(i => $"{{\"name\":\"user{i}\",\"age\":{i}}}");
        string content = string.Join("\n", initialLines.Concat(refinedLines)) + "\n";
        await File.WriteAllTextAsync(path, content);
    }
}
