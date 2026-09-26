using Refedle.App;

namespace Refedle.Tests.App;

internal static class AppStateTestExtensions
{
    /// <summary>
    /// Returns the DrillDown session if one exists, or <c>null</c> — a null-returning form of
    /// <c>TryGetDrillDown</c> that likewise ignores the current mode.
    /// </summary>
    internal static DrillDownState? GetDrillDownOrNull(this AppState state) =>
        state.TryGetDrillDown(out var drillDown) ? drillDown : null;
}
