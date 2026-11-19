// Roslyn resolves [assembly: SuppressMessage] Targets via fully-qualified documentation
// signatures, so member types are spelled out explicitly.
using System.Diagnostics.CodeAnalysis;

// LivePumpTestSession<T>
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.IntegrationTests.App.Tui.UI.LivePumpTestSession`1.InvokeAsync``1(System.Func{Terminal.Gui.App.IApplication,`0,System.Threading.Tasks.Task{``0}})",
    Justification = "Converts any exception from the marshalled action into the TCS's result so the caller observes it via await, instead of it escaping on the loop thread.")]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Refedle.IntegrationTests.App.Tui.UI.LivePumpTestSession`1.CancelAndObserveAsync(System.Threading.CancellationTokenSource,System.Threading.Tasks.Task)",
    Justification = "The startup failure the caller is about to rethrow is what matters; this only prevents pumpTask's own fault from surfacing as an unobserved task exception.")]
