// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified
// documentation-comment signatures (e.g. ~M:N.T.Method(ParamType)), so parameter
// types must be spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// MainWindowTests
[assembly: SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Scope = "type",
    Target = "~T:Refedle.E2ETests.Tui.MainWindow.MainWindowTests",
    Justification = "Cleanup runs through IAsyncLifetime.DisposeAsync, which xUnit guarantees for every test; TuiTestHarness disposal is inherently asynchronous, so a synchronous IDisposable would be redundant.")]
