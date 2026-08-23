using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.App;

/// <summary>
/// Represents the application's global state.
/// </summary>
internal sealed class AppState : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets or sets the current file path being processed.
    /// </summary>
    public string CurrentFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current view mode.
    /// </summary>
    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;

    /// <summary>
    /// Gets or sets the KeyPath of the location currently on screen.
    /// Only meaningful while <see cref="CurrentMode"/> is a Tree mode or FocusedTable — updated
    /// on tree cursor movement and when DrillDown is triggered.
    /// </summary>
    public IReadOnlyList<KeyPathSegment> CurrentKeyPath { get; set; } = [];

    /// <summary>
    /// Gets or sets the table schema for the loaded file.
    /// Null if no file is loaded or schema has not been detected.
    /// </summary>
    public TableSchema? Schema { get; set; }

    /// <summary>
    /// Gets or sets the row indexer for the current file.
    /// Stored on load so it can be reused when switching modes.
    /// </summary>
    public IRowIndexer? RowIndexer { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token source for the background schema scanner.
    /// </summary>
    public CancellationTokenSource Cts { get; private set; } = new();

    /// <summary>
    /// Gets or sets the callback invoked when the background schema scan completes.
    /// Set by <c>ViewManager</c> when creating a table source that supports schema updates;
    /// invoked after background refinement finishes.
    /// </summary>
    public Action<TableSchema>? OnSchemaRefined { get; set; }

    private IReadOnlyList<MorphAction> _actionStack = [];

    /// <summary>
    /// Gets the current Action Stack of transformation operations applied to the loaded file.
    /// An empty list means no transformations are active (passthrough).
    /// Mutation goes through <see cref="AddMorphAction"/>, <see cref="ClearMorphActions"/>, or
    /// <see cref="SetActionStack"/>.
    /// </summary>
    public IReadOnlyList<MorphAction> ActionStack => _actionStack;

    /// <summary>
    /// Gets or sets the DrillDown session state.
    /// Null when not in FocusedTable mode.
    /// </summary>
    public DrillDownState? DrillDown { get; set; }

    /// <summary>
    /// Gets or sets the cached top-level entries for JSON Object tree reconstruction.
    /// Set once at file load for <see cref="DataFormat.JsonObject"/> files; null for all other formats.
    /// </summary>
    public IReadOnlyList<JsonObjectEntry>? JsonObjectEntries { get; set; }

    /// <summary>
    /// Renews the cancellation token source by cancelling the current one and creating a new one.
    /// This should be called when loading a new file to ensure the previous file's background scan
    /// is cancelled and does not interfere with the new file.
    /// </summary>
    public void RenewCtsWithCancel()
    {
        // Cancel must precede Dispose: any captured CancellationToken derived from the old Cts
        // will reflect IsCancellationRequested = true, keeping polling-based checks safe after disposal.
        Cts.Cancel();
        Cts.Dispose();
        Cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Appends a morph action to the Action Stack.
    /// Creates a new <see cref="IReadOnlyList{T}"/> to preserve immutability.
    /// </summary>
    /// <param name="action">The action to append.</param>
    internal void AddMorphAction(MorphAction action)
    {
        _actionStack = [.. _actionStack, action];
    }

    /// <summary>
    /// Clears all morph actions from the Action Stack, resetting it to an empty state.
    /// </summary>
    internal void ClearMorphActions()
    {
        _actionStack = [];
    }

    /// <summary>
    /// Replaces the Action Stack wholesale, e.g. when loading a recipe.
    /// </summary>
    /// <param name="actions">The actions to set as the new Action Stack.</param>
    internal void SetActionStack(IReadOnlyList<MorphAction> actions)
    {
        _actionStack = actions;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cts.Cancel();
        Cts.Dispose();
        _disposed = true;
    }
}
