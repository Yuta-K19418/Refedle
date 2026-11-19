using AwesomeAssertions;
using Refedle.App.Tui.UI.Views.Dialogs;
using Terminal.Gui.Input;

namespace Refedle.IntegrationTests.App.Tui.UI.Views.Dialogs;

public sealed class ActionMenuDialogTests
{
    [Fact]
    public async Task Constructor_WhenUserConfirms_CallsOnConfirmedWithSelectedAction()
    {
        // Arrange
        string[] actions = ["Rename", "Cast", "Delete"];
        string? capturedAction = null;
        await using LivePumpTestSession<ActionMenuDialog> session = await LivePumpTestSession.StartAsync((app, window) =>
        {
            ActionMenuDialog dialog = new(actions, action => capturedAction = action);
            ModalKeys.SendOnceOnTop(app, window, Key.Enter);
            return dialog;
        });

        // Act
        await session.InvokeAsync((app, dialog) =>
        {
            app.Run(dialog);
            return Task.CompletedTask;
        });

        // Assert
        capturedAction.Should().Be("Rename");
    }
}
