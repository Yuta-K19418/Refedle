using Refedle.App.Tui;
using Refedle.Engine.IO;
using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.IO.JsonObject;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Tests.App.Tui;

public sealed partial class AppStateTests
{
    private static readonly IMorphAction RootAction = new RenameColumnAction { OldName = "root", NewName = "root_renamed" };

    private static TableSchema CreateSchema(string columnName = "col1") =>
        new() { SourceFormat = DataFormat.JsonLines, Columns = [new ColumnSchema { Name = columnName, Type = ColumnType.Text }] };

    private static DrillDownState CreateDrillDownState(IMorphAction drillDownAction) =>
        new(
            [new FocusedTableRow(JsonRawBytes.Empty, "[0]")],
            CreateSchema(),
            ViewMode.JsonLinesTree,
            KeyPath: [],
            ActionStack: [drillDownAction]);

    private static AppState CreateStateWithDrillDown(IMorphAction drillDownAction)
    {
        AppState state = new();
        state.EnterFocusedTable(CreateDrillDownState(drillDownAction));
        return state;
    }

    // Root state whose Action Stack is saved, so a change to either root property stands out.
    private static AppState CreateSavedRootState(AppState state)
    {
        state.AddMorphAction(RootAction);
        state.MarkRecipeSaved();
        return state;
    }

    // A state as left by a previous session: a CSV load, then a JSON Object load, unsaved root
    // actions and a DrillDown session.
    private static AppState CreatePopulatedState()
    {
        AppState state = new();
        state.StartNewFile("old.csv");
        state.CompleteCsvLoad(new AppStateTestRowIndexer("old.csv"), CreateSchema("old"));
        state.SetSchemaRefinedCallback(_ => { });
        state.SetCurrentKeyPath([new KeyPathSegment("k", KeyPathSegmentKind.Key)]);
        state.EnterJsonObjectTree([new JsonObjectEntry("id", "1"u8.ToArray())]);
        state.AddMorphAction(RootAction);
        state.EnterFocusedTable(CreateDrillDownState(new DeleteColumnAction { ColumnName = "c" }));
        return state;
    }

    // The populated state after its DrillDown session ended: a tree mode with no session.
    private static AppState CreatePopulatedTreeState()
    {
        AppState state = CreatePopulatedState();
        state.ExitFocusedTable(ViewMode.JsonObjectTree);
        return state;
    }

    private static (string, IReadOnlyList<KeyPathSegment>, TableSchema?, IRowIndexer?, CancellationTokenSource, Action<TableSchema>?, IReadOnlyList<IMorphAction>, bool, IReadOnlyList<JsonObjectEntry>?)
        CaptureExceptMode(AppState state) =>
        (state.CurrentFilePath, state.CurrentKeyPath, state.Schema, state.RowIndexer, state.Cts,
            state.OnSchemaRefined, state.ActionStack, state.HasUnsavedChanges, state.JsonObjectEntries);

    private sealed class AppStateTestRowIndexer(string filePath) : IRowIndexer
    {
        public string FilePath => filePath;
        public long FileSize => 1000;
        public long BytesRead => 1000;
        public long TotalRows => 10;
        public bool IsIndexingCompleted => true;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void BuildIndex(CancellationToken cancellationToken = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}
