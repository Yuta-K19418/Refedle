using AwesomeAssertions;
using Refedle.App.Tui;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    [Fact]
    public void MarkRecipeSaved_AfterMutation_ClearsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });

        // Act
        state.MarkRecipeSaved();

        // Assert
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkRecipeSavedIfUnchanged_WhenRevisionMatches_ClearsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        long savedRevision = state.Revision;

        // Act
        state.MarkRecipeSavedIfUnchanged(savedRevision);

        // Assert
        state.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void MarkRecipeSavedIfUnchanged_WhenStackMutatedSinceCapture_KeepsUnsavedChanges()
    {
        // Arrange
        using AppState state = new();
        state.AddMorphAction(new RenameColumnAction { OldName = "a", NewName = "b" });
        long savedRevision = state.Revision;
        state.ClearMorphActions();

        // Act
        state.MarkRecipeSavedIfUnchanged(savedRevision);

        // Assert
        state.HasUnsavedChanges.Should().BeTrue();
    }
}
