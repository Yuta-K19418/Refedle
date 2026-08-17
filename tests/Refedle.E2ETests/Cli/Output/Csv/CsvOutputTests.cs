using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Csv;

public sealed partial class CsvOutputTests : IDisposable
{
    private const string TestCsvContent = """
        name,age
        Alice,30
        Bob,25
        Charlie,35
        """;

    private readonly TestDirectory _testDirectory = new();

    public void Dispose()
    {
        _testDirectory.Dispose();
    }
}
