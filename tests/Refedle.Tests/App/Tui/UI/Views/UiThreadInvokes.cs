
namespace Refedle.Tests.App.Tui.UI.Views;

/// <summary>UI-thread invoke stub that runs the action inline.</summary>
internal static class UiThreadInvokes
{
    internal static void SynchronousUiThreadInvoke(Action action) => action();
}
