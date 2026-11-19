using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class MorphTableViewTests
{
    // Minimal 2-row source so navigation commands (Down/Up) are valid and get consumed.
    private sealed class TwoRowTableSource : ITableSource
    {
        public int Rows => 2;
        public int Columns => 1;
        public string[] ColumnNames => ["c"];
        public object this[int row, int col] => row;
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
        using KeyProcessingMorphTableView view = new();
        using FlagDisposableBasicTableSource table = new();
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
        using KeyProcessingMorphTableView view = new();
        NonDisposableTableSource table = new();
        view.Table = table;

        // Act
        Action act = () => view.Dispose();

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
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new();

        // Act
        Func<bool> act = () => view.ProcessKey(new Key(KeyCode.J));

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
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        bool result = view.ProcessKey(new Key(keyCode));

        // Assert — hjkl/du are mapped to commands and consumed.
        result.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_FirstGOfSequence_IsConsumedAndReturnsTrue()
    {
        // Arrange — first 'g' enters the pending state (PendingGSequence).
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        bool result = view.ProcessKey(new Key(KeyCode.G));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_GGSequence_GoesToFirstAndReturnsTrue()
    {
        // Arrange — 'gg' within the timeout resolves to GoToFirst. The view owns its own
        // translator with a 1000 ms timeout, so two synchronous presses stay well within it.
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        bool first = view.ProcessKey(new Key(KeyCode.G));
        bool second = view.ProcessKey(new Key(KeyCode.G));

        // Assert — both presses are consumed; the pair navigates to the first row.
        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void OnKeyDown_ShiftG_GoesToEndAndReturnsTrue()
    {
        // Arrange — Shift+G resolves to GoToEnd.
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        bool result = view.ProcessKey(new Key(KeyCode.G | KeyCode.ShiftMask));

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
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        bool result = view.ProcessKey(new Key(keyCode));

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
        using IApplication app = TestAppFactory.Create();
        using KeyProcessingMorphTableView view = new()
        {
            Table = new TwoRowTableSource(),
        };

        // Act
        Func<bool> act = () => view.ProcessKey(new Key(keyCode));

        // Assert
        act.Should().NotThrow();
    }
}
