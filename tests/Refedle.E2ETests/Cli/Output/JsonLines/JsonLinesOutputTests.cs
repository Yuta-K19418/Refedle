using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.JsonLines;

public sealed partial class JsonLinesOutputTests : IDisposable
{
    private readonly TestDirectory _testDirectory = new();

    public void Dispose()
    {
        _testDirectory.Dispose();
    }
}
