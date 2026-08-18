using Refedle.App;
using Terminal.Gui.Drivers;

namespace Refedle.E2ETests.Tui.MainWindow;

public sealed partial class MainWindowTests
{
    [Fact]
    public async Task QuitKey_OnCsvTableWithNoPendingActions_StopsTheEventLoop()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("Charlie", "35");

        // Act & Assert: an empty action stack means no confirmation dialog is shown.
        Harness.SendKey(KeyCode.Q);
        await Harness.WaitForExitAsync();
    }

    [Fact]
    public async Task QuitKey_OnJsonArrayTreeWithNoPendingActions_StopsTheEventLoop()
    {
        // Arrange
        var content = """[{"name":"Item1","age":1}]""";
        var inputFile = _testDirectory.CreateFile("input.json", content);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("[0]:");

        // Act & Assert: an empty action stack means no confirmation dialog is shown.
        Harness.SendKey(KeyCode.Q);
        await Harness.WaitForExitAsync();
    }

    [Fact]
    public async Task QuitKey_OnCsvTableWithPendingActions_ConfirmingDialogStopsTheEventLoop()
    {
        // Arrange
        var csvContent = """
            name,age
            Alice,30
            Bob,25
            Charlie,35
            """;
        var inputFile = _testDirectory.CreateFile("input.csv", csvContent);
        Harness.MainWindow.ScheduleStartupLoad(new TuiStartupOptions(inputFile));
        await Harness.WaitForContentsAsync("Charlie", "35");

        // Rename a column first so the action stack is non-empty and 'q' shows a
        // confirmation instead of quitting directly.
        Harness.SendKey(KeyCode.X);
        Harness.SendKey(KeyCode.Enter);
        // The text field is pre-filled with the current name "name" (4 chars); clear it
        // before typing the replacement.
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendKey(KeyCode.Backspace);
        Harness.SendText("years");
        // Queued keys drain one per UI-loop iteration, so typing a multi-character value can take
        // longer than a short fixed delay would allow; wait for it to fully land before submitting.
        await Harness.WaitForContentsAsync("New name: years");
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForContentsAsync("years (text)");

        // Act & Assert: the "Quit anyway?" confirmation defaults to No, so Left moves focus
        // to Yes before Enter confirms quitting and stops the event loop.
        Harness.SendKey(KeyCode.Q);
        await Harness.WaitForContentsAsync("Quit anyway?");
        Harness.SendKey(KeyCode.CursorLeft);
        Harness.SendKey(KeyCode.Enter);
        await Harness.WaitForExitAsync();
    }
}
