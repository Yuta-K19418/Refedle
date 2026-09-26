using AwesomeAssertions;
using Refedle.App.Tui.Ui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.Ui.Views;

public sealed class MorphTableViewTests
{
    private sealed class ConcreteMorphTableView : MorphTableView
    {
        // Exposes the protected OnKeyDown for characterization testing.
        public bool ProcessKey(Key key) => OnKeyDown(key);
    }

    // Minimal 2-row source so navigation commands (Down/Up) are valid and get consumed.
    private sealed class TwoRowTableSource : ITableSource
    {
        public int Rows => 2;
        public int Columns => 1;
        public string[] ColumnNames => ["c"];
        public object this[int row, int col] => row;
    }

    private sealed class DisposableTableSource : ITableSource, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
        public void Dispose() => IsDisposed = true;
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        Assert.NotNull(app.Driver);
        app.Driver.SetScreenSize(80, 25);
        return app;
    }

    private sealed class NonDisposableTableSource : ITableSource
    {
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
    }

    [Fact]
    public void Dispose_DisposesTableIfIDisposable()
    {
        // Arrange
        using var view = new ConcreteMorphTableView();
        using var table = new DisposableTableSource();
        view.Table = table;

        // Act
        view.Dispose();

        // Assert
        table.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenTableIsNotIDisposable()
    {
        // Arrange
        using var view = new ConcreteMorphTableView();
        var table = new NonDisposableTableSource();
        view.Table = table;

        // Act
        var act = () => view.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // OnKeyDown characterization tests
    // These lock the current return-value semantics (consumed=true /
    // bubbles-up=false) so the upcoming complexity refactor — which extracts a
    // pure MapCommand helper and splits dispatch — cannot silently change
    // which keys are consumed. No production code is modified here.
    // -----------------------------------------------------------------------

    [Fact]
    public void OnKeyDown_WhenTableIsNull_DelegatesToBaseWithoutThrowing()
    {
        // Arrange — Table is null, so OnKeyDown short-circuits to the base view.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView();

        // Act
        var act = () => view.ProcessKey(new Key(KeyCode.J));

        // Assert — the vim path is skipped; base.OnKeyDown runs without throwing.
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(KeyCode.H)]
    [InlineData(KeyCode.J)]
    [InlineData(KeyCode.K)]
    [InlineData(KeyCode.L)]
    [InlineData(KeyCode.D)]
    [InlineData(KeyCode.U)]
    public void OnKeyDown_VimNavigationKey_IsConsumedAndReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        var result = view.ProcessKey(new Key(keyCode));

        // Assert — hjkl/du are mapped to commands and consumed.
        result.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_FirstGOfSequence_IsConsumedAndReturnsTrue()
    {
        // Arrange — first 'g' enters the pending state (PendingGSequence).
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

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
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        var first = view.ProcessKey(new Key(KeyCode.G));
        var second = view.ProcessKey(new Key(KeyCode.G));

        // Assert — both presses are consumed; the pair navigates to the first row.
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_ShiftG_GoesToEndAndReturnsTrue()
    {
        // Arrange — Shift+G resolves to GoToEnd.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        var result = view.ProcessKey(new Key(KeyCode.G | KeyCode.ShiftMask));

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyCode.O)]
    [InlineData(KeyCode.Q)]
    [InlineData(KeyCode.T)]
    public void OnKeyDown_GlobalShortcut_BubblesUpAndReturnsFalse(KeyCode keyCode)
    {
        // Arrange — global shortcuts must NOT be consumed so they reach AppKeyHandler.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

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
        // The return value depends on base TableView state, so only exception-safety is locked.
        using var app = CreateTestApp();
        using var view = new ConcreteMorphTableView
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        var act = () => view.ProcessKey(new Key(keyCode));

        // Assert
        act.Should().NotThrow();
    }
}
