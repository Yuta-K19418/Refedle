using AwesomeAssertions;
using Refedle.TestHelpers.App.Tui.UI;
using static Refedle.TestHelpers.App.Tui.UI.LazyFilterScanTrackerHarnessAssertions;

namespace Refedle.IntegrationTests.App.Tui.UI;

// LazyFilterScanTracker in isolation: the deferred start behind the row-indexing wait, the
// overlay lifecycle on the CSV / JSON Lines table views, progress handling, and the
// stale-notification discard for every retirement path.
public sealed class LazyFilterScanTrackerTests
{
    [Fact]
    public async Task StartScan_PlacesOverlayAtHostBottomRegardlessOfTableRowCount()
    {
        // Arrange — the table content (50k rows) is far taller than the host viewport; the
        // session owns the application and its loop, so layout runs on the loop thread
        await using LivePumpTestSession<LazyFilterScanTrackerHarness> session = await LivePumpTestSession.StartAsync(app =>
        {
            LazyFilterScanTrackerHarness harness = new(totalRows: 50_000, externalApp: app);
            return (harness.Top, harness);
        });

        // Act — UI work runs in synchronous delegates on the loop thread; only the thread-safe
        // CompleteIndexing and the bounded queue read happen on the test thread
        LazyFilterScanTrackerHarness harness = await session.InvokeAsync((app, h) =>
        {
            app.LayoutAndDraw();
            _ = h.StartScanAsync();
            return Task.FromResult(h);
        });
        harness.CompleteIndexing();
        Action beginScan = await harness.Pending.NextAsync();
        await harness.Pending.RunPendingAsync(expectedCount: 0); // the scan start is the only queued callback
        (int hostContentHeight, int tableContentHeight, int overlayY) = await session.InvokeAsync((app, h) =>
        {
            beginScan();
            app.LayoutAndDraw();

            return Task.FromResult((
                h.OverlayHost.GetContentSize().Height,
                h.View.GetContentSize().Height,
                GetOverlayLabel(h).Frame.Y));
        });

        // Assert — the strip stays on the host's bottom row even though the table content
        // extends far below the viewport
        tableContentHeight.Should().BeGreaterThan(hostContentHeight);
        overlayY.Should().Be(hostContentHeight - 1);
    }
}
