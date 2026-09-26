using System.Diagnostics.CodeAnalysis;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.App;

/// <summary>
/// Represents the application's global state.
/// Properties are read-only outside this class; every mutation goes through an instance method
/// named after the session event it applies, so related properties change together.
/// </summary>
internal sealed class AppState : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the current file path being processed.
    /// </summary>
    public string CurrentFilePath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the current view mode.
    /// </summary>
    public ViewMode CurrentMode { get; private set; } = ViewMode.FileSelection;

    /// <summary>
    /// Gets the KeyPath of the location currently on screen.
    /// Only meaningful while <see cref="CurrentMode"/> is a Tree mode or FocusedTable — updated
    /// on tree cursor movement and when DrillDown is triggered.
    /// </summary>
    public IReadOnlyList<KeyPathSegment> CurrentKeyPath { get; private set; } = [];

    /// <summary>
    /// Gets the table schema for the loaded file.
    /// Null if no file is loaded or schema has not been detected.
    /// </summary>
    public TableSchema? Schema { get; private set; }

    /// <summary>
    /// Gets the row indexer for the current file.
    /// Stored on load so it can be reused when switching modes.
    /// </summary>
    public IRowIndexer? RowIndexer { get; private set; }

    /// <summary>
    /// Gets the cancellation token source for the background schema scanner.
    /// </summary>
    public CancellationTokenSource Cts { get; private set; } = new();

    /// <summary>
    /// Gets the callback invoked when the background schema scan completes.
    /// Set by <c>ViewManager</c> when creating a table source that supports schema updates;
    /// invoked after background refinement finishes.
    /// </summary>
    public Action<TableSchema>? OnSchemaRefined { get; private set; }

    private IReadOnlyList<MorphAction> _actionStack = [];

    /// <summary>
    /// Gets the current Action Stack of transformation operations applied to the loaded file.
    /// An empty list means no transformations are active (passthrough).
    /// Mutation goes through <see cref="AddMorphAction"/>, <see cref="ClearMorphActions"/>, or
    /// <see cref="SetActionStack"/>.
    /// </summary>
    public IReadOnlyList<MorphAction> ActionStack => _actionStack;

    private bool _hasUnsavedChanges;
    private long _revision;

    /// <summary>
    /// Gets whether the root Action Stack contains changes not reflected in a recipe file.
    /// Every mutation sets this. It is cleared by <see cref="MarkRecipeSaved"/> (recipe load, new-file
    /// reset) and by <see cref="MarkRecipeSavedIfUnchanged"/> (successful root save, with the
    /// <see cref="Revision"/> captured before the write). Unlike the stack count, this stays false
    /// right after a successful save or load.
    /// <para>
    /// Threading: every AppState read and write is UI-thread-only (reach it through
    /// <c>IApplication.Invoke</c>). No synchronization is provided.
    /// </para>
    /// </summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    /// <summary>
    /// Gets a counter that increases on every root Action Stack mutation. A save captures it
    /// before its asynchronous write and passes it to <see cref="MarkRecipeSavedIfUnchanged"/>, so
    /// edits made while the write was in flight are not reported as saved.
    /// </summary>
    public long Revision => _revision;

    /// <summary>
    /// Gets the DrillDown session state. Kept private so callers ask <see cref="TryGetDrillDown"/>
    /// instead of null-checking it. A session can outlive FocusedTable mode (an error view
    /// replaces it without clearing it), so it is not tied to <see cref="CurrentMode"/>.
    /// </summary>
    private DrillDownState? DrillDown { get; set; }

    /// <summary>
    /// Gets a value indicating whether the current view is FocusedTable. Says nothing about
    /// whether a session exists; pair it with <see cref="TryGetDrillDown"/> when both matter.
    /// </summary>
    public bool IsDrillDownMode => CurrentMode == ViewMode.FocusedTable;

    /// <summary>
    /// Gets the DrillDown session if one exists, regardless of <see cref="CurrentMode"/>.
    /// </summary>
    /// <param name="drillDown">The session when this returns <c>true</c>; otherwise <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> when a session exists — including a stale one left over after an error view
    /// replaced FocusedTable.
    /// </returns>
    internal bool TryGetDrillDown([NotNullWhen(true)] out DrillDownState? drillDown)
    {
        drillDown = DrillDown;
        return drillDown is not null;
    }

    /// <summary>
    /// Gets the cached top-level entries for JSON Object tree reconstruction.
    /// Set once at file load for <see cref="DataFormat.JsonObject"/> files; null for all other formats.
    /// </summary>
    public IReadOnlyList<JsonObjectEntry>? JsonObjectEntries { get; private set; }

    /// <summary>
    /// Starts a session for a newly selected file: installs the new file path, resets the
    /// previous session's transforms, unsaved-changes flag, background scans, DrillDown session,
    /// and cached JSON Object entries.
    /// </summary>
    /// <param name="path">The path of the newly selected file.</param>
    internal void StartNewFile(string path)
    {
        CurrentFilePath = path;
        ClearMorphActions();
        // The fresh session starts clean: an empty stack cannot diverge from anything on disk.
        MarkRecipeSaved();
        RenewCtsWithCancel();
        DrillDown = null;
        JsonObjectEntries = null;
    }

    /// <summary>
    /// Begins loading an indexer-backed tree file (JSON Lines / JSON Array): stores the new
    /// indexer and discards the previous file's table state.
    /// </summary>
    /// <param name="indexer">The row indexer created for the new file.</param>
    internal void BeginIndexedTreeLoad(IRowIndexer indexer)
    {
        RowIndexer = indexer;
        Schema = null;
        OnSchemaRefined = null;
    }

    /// <summary>
    /// Begins loading a JSON Object file: JSON Object has no rows, so the indexer and the
    /// previous file's table state are discarded.
    /// </summary>
    internal void BeginJsonObjectLoad()
    {
        RowIndexer = null;
        Schema = null;
        OnSchemaRefined = null;
    }

    /// <summary>
    /// Completes a CSV load with the initial scan results: publishes the schema and indexer and
    /// enters the CSV table mode.
    /// </summary>
    /// <param name="indexer">The CSV row indexer for the loaded file.</param>
    /// <param name="schema">The schema produced by the initial scan.</param>
    internal void CompleteCsvLoad(IRowIndexer indexer, TableSchema schema)
    {
        Schema = schema;
        RowIndexer = indexer;
        CurrentMode = ViewMode.CsvTable;
    }

    /// <summary>
    /// Completes a JSON Object load with the scanned top-level entries and enters its tree mode.
    /// </summary>
    /// <param name="entries">The key-value pairs returned by <see cref="Engine.IO.JsonObject.TopLevelScanner.Scan"/>.</param>
    internal void EnterJsonObjectTree(IReadOnlyList<JsonObjectEntry> entries)
    {
        JsonObjectEntries = entries;
        CurrentMode = ViewMode.JsonObjectTree;
    }

    /// <summary>
    /// Enters the JSON Lines tree mode — after the first checkpoint of a load, on a
    /// Tree/Table toggle back to the tree, or when returning from a DrillDown.
    /// </summary>
    internal void EnterJsonLinesTree()
    {
        CurrentMode = ViewMode.JsonLinesTree;
    }

    /// <summary>
    /// Enters the JSON Array tree mode — after the first checkpoint of a load or when returning
    /// from a DrillDown.
    /// </summary>
    internal void EnterJsonArrayTree()
    {
        CurrentMode = ViewMode.JsonArrayTree;
    }

    /// <summary>
    /// Enters the JSON Lines table mode from its tree mode when the schema is already cached.
    /// </summary>
    internal void EnterJsonLinesTable()
    {
        CurrentMode = ViewMode.JsonLinesTable;
    }

    /// <summary>
    /// Completes the initial schema scan on the first Tree→Table toggle: publishes the scanned
    /// schema and enters the JSON Lines table mode.
    /// </summary>
    /// <param name="schema">The schema produced by the initial scan.</param>
    internal void CompleteJsonLinesSchemaScan(TableSchema schema)
    {
        Schema = schema;
        CurrentMode = ViewMode.JsonLinesTable;
    }

    /// <summary>
    /// Enters the placeholder mode used for error reporting; other session state is kept intact.
    /// </summary>
    internal void EnterPlaceholderMode()
    {
        CurrentMode = ViewMode.PlaceholderView;
    }

    /// <summary>
    /// Enters FocusedTable mode with the given DrillDown session as its backing state.
    /// </summary>
    /// <param name="drillDown">The DrillDown session to display.</param>
    internal void EnterFocusedTable(DrillDownState drillDown)
    {
        DrillDown = drillDown;
        CurrentMode = ViewMode.FocusedTable;
    }

    /// <summary>
    /// Leaves FocusedTable mode: clears the DrillDown session and restores the mode the session
    /// was entered from.
    /// </summary>
    /// <param name="modeToRestore">The tree mode recorded as the DrillDown's previous mode.</param>
    internal void ExitFocusedTable(ViewMode modeToRestore)
    {
        DrillDown = null;
        CurrentMode = modeToRestore;
    }

    /// <summary>
    /// Appends a morph action to the DrillDown session's Action Stack and flags the session unsaved.
    /// Does nothing when no session exists, regardless of <see cref="CurrentMode"/>.
    /// </summary>
    /// <param name="action">The action to append.</param>
    internal void AddDrillDownAction(MorphAction action)
    {
        if (DrillDown is not { } drillDown)
        {
            return;
        }

        DrillDown = drillDown with
        {
            ActionStack = [.. drillDown.ActionStack, action],
            HasUnsavedChanges = true,
        };
    }

    /// <summary>
    /// Clears the DrillDown session's Action Stack and flags the session unsaved.
    /// Does nothing when no session exists, regardless of <see cref="CurrentMode"/>.
    /// </summary>
    internal void ClearDrillDownActions()
    {
        if (DrillDown is not { } drillDown)
        {
            return;
        }

        DrillDown = drillDown with { ActionStack = [], HasUnsavedChanges = true };
    }

    /// <summary>
    /// Clears the DrillDown session's unsaved-changes flag after its recipe was saved.
    /// Does nothing when no session exists, regardless of <see cref="CurrentMode"/>.
    /// </summary>
    internal void MarkDrillDownSaved()
    {
        if (DrillDown is not { } drillDown)
        {
            return;
        }

        DrillDown = drillDown with { HasUnsavedChanges = false };
    }

    /// <summary>
    /// Publishes a schema refined by the background scan while the session is still current.
    /// </summary>
    /// <param name="schema">The refined schema.</param>
    internal void ApplyRefinedSchema(TableSchema schema)
    {
        Schema = schema;
    }

    /// <summary>
    /// Registers the callback that receives refined schemas, or clears it by passing <c>null</c>
    /// when the current view cannot consume schema updates.
    /// </summary>
    /// <param name="callback">The callback, or <c>null</c> to clear it.</param>
    internal void SetSchemaRefinedCallback(Action<TableSchema>? callback)
    {
        OnSchemaRefined = callback;
    }

    /// <summary>
    /// Stores the on-screen location reported by breadcrumb updates; an empty path clears it.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the current location.</param>
    internal void SetCurrentKeyPath(IReadOnlyList<KeyPathSegment> path)
    {
        CurrentKeyPath = path;
    }

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
        _hasUnsavedChanges = true;
        _revision++;
    }

    /// <summary>
    /// Clears all morph actions from the Action Stack, resetting it to an empty state.
    /// </summary>
    internal void ClearMorphActions()
    {
        // An already-empty stack is unchanged: flagging it would raise a false unsaved-changes prompt.
        if (_actionStack.Count == 0)
        {
            return;
        }

        _actionStack = [];
        _hasUnsavedChanges = true;
        _revision++;
    }

    /// <summary>
    /// Replaces the Action Stack wholesale, e.g. when loading a recipe.
    /// </summary>
    /// <param name="actions">The actions to set as the new Action Stack.</param>
    internal void SetActionStack(IReadOnlyList<MorphAction> actions)
    {
        _actionStack = actions;
        _hasUnsavedChanges = true;
        _revision++;
    }

    /// <summary>
    /// Unconditionally clears <see cref="HasUnsavedChanges"/>. For resets only: after a recipe load
    /// (the stack then mirrors the recipe file) and when opening a new file resets the session.
    /// A root recipe save must use <see cref="MarkRecipeSavedIfUnchanged"/> instead.
    /// </summary>
    internal void MarkRecipeSaved()
    {
        _hasUnsavedChanges = false;
    }

    /// <summary>
    /// Clears <see cref="HasUnsavedChanges"/> only when <see cref="Revision"/> still equals
    /// <paramref name="savedRevision"/>, i.e. the root Action Stack is exactly what was persisted.
    /// </summary>
    /// <param name="savedRevision">The <see cref="Revision"/> captured before the save began.</param>
    internal void MarkRecipeSavedIfUnchanged(long savedRevision)
    {
        if (_revision == savedRevision)
        {
            _hasUnsavedChanges = false;
        }
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
