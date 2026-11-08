using AwesomeAssertions;
using Refedle.App.Tui.UI;
using Refedle.App.Tui.UI.Views;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;
using Terminal.Gui.Views;

namespace Refedle.Tests.App.Tui.UI;

public sealed class FilterScanSessionTests
{
    private sealed class FakeFocusedSource(string[][] data, string[] columnNames) : IExtendedTableSource
    {
        public long TotalRows => data.Length;
        public int Columns => columnNames.Length;
        public string[] ColumnNames => columnNames;
        public string[] RawColumnNames => columnNames;
        public object this[long row, int col] => data[(int)row][col];
    }

    private sealed class Fixture : IDisposable
    {
        private int _progressCalls;

        public Fixture()
        {
            var source = new FakeFocusedSource(
                [["[0]", "A"], ["[1]", "B"]],
                ["#", "name"]);
            var schema = new TableSchema
            {
                Columns = [new ColumnSchema { Name = "name", Type = ColumnType.Text, ColumnIndex = 0 }],
                SourceFormat = DataFormat.JsonArray,
            };
            IReadOnlyList<IMorphAction> actions =
                [FilterAction.Create("name", FilterOperator.Equals, ComparisonType.Text, "B").Value];
            Transformer = FocusedTableTransformer.Create(source, schema, actions, 200);
            Indexer = Transformer.FilterRowIndexer.Should().BeOfType<FocusedTableFilterRowIndexer>().Subject;
            Action<int, int> handler = (_, _) => Interlocked.Increment(ref _progressCalls);
            Indexer.ProgressChanged += handler;
            Cts = new CancellationTokenSource();
            Session = new FilterScanSession(Cts, Indexer, Transformer, new TableStyle(), handler);
        }

        public FilterScanSession Session { get; }
        public FocusedTableTransformer Transformer { get; }
        public FocusedTableFilterRowIndexer Indexer { get; }
        public CancellationTokenSource Cts { get; }
        public int ProgressCalls => Volatile.Read(ref _progressCalls);

        public void Dispose() => Session.Dispose();
    }

    [Fact]
    public async Task Dispose_Once_UnsubscribesProgressDisposesCtsAndTransformer()
    {
        // Arrange
        using var fixture = new Fixture();

        // Act
        fixture.Session.Dispose();
        await fixture.Indexer.BuildIndexAsync(CancellationToken.None);

        // Assert
        fixture.ProgressCalls.Should().Be(0);
        var readCtsToken = () => fixture.Cts.Token;
        readCtsToken.Should().Throw<ObjectDisposedException>();
        var readDisposedTransformer = () => fixture.Transformer[0, 1];
        readDisposedTransformer.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        using var fixture = new Fixture();
        fixture.Session.Dispose();

        // Act
        var disposeAgain = () => fixture.Session.Dispose();

        // Assert
        disposeAgain.Should().NotThrow();
    }
}
