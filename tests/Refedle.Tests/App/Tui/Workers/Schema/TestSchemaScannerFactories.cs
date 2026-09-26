using Refedle.App.Tui.Workers.Schema;

namespace Refedle.Tests.App.Tui.Workers.Schema;

/// <summary>
/// Factories that create the real schema scanners for tests that do not need to control scan timing.
/// </summary>
internal static class TestSchemaScannerFactories
{
    public static ISchemaScanner JsonLines(string path) =>
        new Refedle.App.Tui.Workers.Schema.JsonLines.IncrementalSchemaScanner(path);

    public static ISchemaScanner Csv(string path) =>
        new Refedle.App.Tui.Workers.Schema.Csv.IncrementalSchemaScanner(path);
}
