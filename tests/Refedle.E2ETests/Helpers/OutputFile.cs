namespace Refedle.E2ETests.Helpers;

internal static class OutputFile
{
    public static async Task<IReadOnlyList<string>> ReadLinesAsync(string path)
    {
        return await File.ReadAllLinesAsync(path).ConfigureAwait(false);
    }
}
