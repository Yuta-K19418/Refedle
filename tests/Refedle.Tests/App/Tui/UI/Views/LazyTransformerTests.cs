using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Filtering;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class LazyTransformerTests
{
    /// <summary>
    /// A synchronous stub that returns pre-computed matched row indices without any filtering logic.
    /// </summary>
    private sealed class SyncFilterRowIndexer(IReadOnlyList<int> matchedRows) : IFilterRowIndexer
    {
#pragma warning disable CS0067
        public event Action<int, int>? ProgressChanged;
#pragma warning restore CS0067

        public int TotalMatchedRows => matchedRows.Count;

        public int GetSourceRow(int filteredIndex) => matchedRows[filteredIndex];

        public Task BuildIndexAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static LazyTransformer MakeFilteredTransformer(
        StringRowsFakeTableSource source,
        TableSchema schema,
        IReadOnlyList<IMorphAction> actions,
        IReadOnlyList<int> matchedRows
    ) => LazyTransformer.Create(source, schema, actions, _ => new SyncFilterRowIndexer(matchedRows));
}
