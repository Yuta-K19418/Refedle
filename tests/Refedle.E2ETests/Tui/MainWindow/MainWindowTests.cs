using Refedle.E2ETests.Helpers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests : IAsyncLifetime
{
    private readonly TestDirectory _testDirectory = new();
    private TuiTestHarness? _harness;

    private TuiTestHarness Harness =>
        _harness ?? throw new InvalidOperationException("InitializeAsync has not completed yet.");

    public async Task InitializeAsync()
    {
        _harness = await TuiTestHarness.StartAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Harness.DisposeAsync();
        }
        finally
        {
            _testDirectory.Dispose();
        }
    }
}
