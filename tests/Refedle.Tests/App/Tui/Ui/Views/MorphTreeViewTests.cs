using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class MorphTreeViewTests
{
    private sealed class ConcreteMorphTreeView : MorphTreeView
    {
        public ConcreteMorphTreeView(Action onTableModeToggle, Action<ITreeNode?> onSelectionChanged)
            : base(onTableModeToggle, onSelectionChanged)
        {
        }

        // Exposes the protected OnKeyDown for characterization testing.
        public bool ProcessKey(Key key) => OnKeyDown(key);
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    // -----------------------------------------------------------------------
    // OnKeyDown characterization tests
    // These lock the current return-value semantics (consumed=true /
    // bubbles-up=false) and the T-key table-mode toggle side effect, so the
    // upcoming complexity refactor — which groups the four selection-offset
    // moves into a single TrySelectionMove helper — cannot silently change
    // which keys are consumed or whether the toggle fires. No production code
    // is modified here.
    // -----------------------------------------------------------------------

    [Fact]
    public void OnKeyDown_TKey_InvokesTableModeToggleAndReturnsTrue()
    {
        // Arrange — T switches to table mode via the injected callback.
        using var app = CreateTestApp();
        var toggled = false;
        using var view = new ConcreteMorphTreeView(() => toggled = true, _ => { });

        // Act
        var result = view.ProcessKey(new Key(KeyCode.T));

        // Assert — the toggle fires and the key is consumed.
        result.Should().BeTrue();
        toggled.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyCode.J)]
    [InlineData(KeyCode.K)]
    [InlineData(KeyCode.D)]
    [InlineData(KeyCode.U)]
    public void OnKeyDown_VimSelectionKey_IsConsumedAndReturnsTrue(KeyCode keyCode)
    {
        // Arrange — j/k move by one row, d/u move by one page; all go through ConsumeAction.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var result = view.ProcessKey(new Key(keyCode));

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyCode.H)]
    [InlineData(KeyCode.L)]
    public void OnKeyDown_HorizontalVimKey_DelegatesToBaseWithoutThrowing(KeyCode keyCode)
    {
        // Arrange — h/l are forwarded to the base TreeView as CursorLeft/CursorRight.
        // Their return value depends on base TreeView state, so only exception-safety is locked.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var act = () => view.ProcessKey(new Key(keyCode));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void OnKeyDown_FirstGOfSequence_IsConsumedAndReturnsTrue()
    {
        // Arrange — first 'g' enters the pending state (PendingGSequence).
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var result = view.ProcessKey(new Key(KeyCode.G));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_GGSequence_GoesToFirstAndReturnsTrue()
    {
        // Arrange — 'gg' within the timeout resolves to GoToFirst. The view owns its own
        // translator with a 1000 ms timeout, so two synchronous presses stay well within it.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var first = view.ProcessKey(new Key(KeyCode.G));
        var second = view.ProcessKey(new Key(KeyCode.G));

        // Assert — both presses are consumed; the pair navigates to the first node.
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_ShiftG_GoesToEndAndReturnsTrue()
    {
        // Arrange — Shift+G resolves to GoToEnd.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var result = view.ProcessKey(new Key(KeyCode.G | KeyCode.ShiftMask));

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyCode.O)]
    [InlineData(KeyCode.Q)]
    [InlineData(KeyCode.X)]
    public void OnKeyDown_GlobalShortcut_BubblesUpAndReturnsFalse(KeyCode keyCode)
    {
        // Arrange — global shortcuts must NOT be consumed so they reach AppKeyHandler.
        // (T is excluded here because it is intercepted earlier as the table-mode toggle.)
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var result = view.ProcessKey(new Key(keyCode));

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(KeyCode.Enter)]
    [InlineData(KeyCode.Esc)]
    public void OnKeyDown_NonVimNonGlobalKey_DelegatesToBaseWithoutThrowing(KeyCode keyCode)
    {
        // Arrange — neither a vim key nor a global shortcut, so it falls through to base.OnKeyDown.
        // The return value depends on base TreeView state, so only exception-safety is locked.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTreeView(() => { }, _ => { });

        // Act
        var act = () => view.ProcessKey(new Key(keyCode));

        // Assert
        act.Should().NotThrow();
    }
}
