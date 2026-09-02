using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Cli.Output.Json;

public sealed partial class JsonOutputTests : IDisposable
{
    private readonly TestDirectory _testDirectory = new();

    public void Dispose()
    {
        _testDirectory.Dispose();
    }
}
