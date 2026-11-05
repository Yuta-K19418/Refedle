using System.Runtime.CompilerServices;

namespace Refedle.Tests;

/// <summary>
/// Assembly-level setup that the runtime executes exactly once, before any other code in
/// this assembly (including static constructors and xUnit fixture creation), so it is
/// guaranteed to run before the first <c>Application.Init</c> in
/// <see cref="App.Tui.UI.TestAppFactory"/> or any test class.
/// </summary>
/// <remarks>
/// Terminal.Gui's ANSI input pipeline picks between real-console I/O and a degraded
/// buffer-only mode once per process. On Windows CI, real-console mode can hang
/// <c>ApplicationImpl.Dispose()</c> forever because the input thread stays blocked in
/// Win32 <c>ReadFile</c>. Forcing headless mode up front keeps every test in this
/// assembly off the real console, mirroring <c>Refedle.E2ETests</c>' TuiTestHarness.
/// </remarks>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void DisableRealDriverIO() =>
        Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");
}
