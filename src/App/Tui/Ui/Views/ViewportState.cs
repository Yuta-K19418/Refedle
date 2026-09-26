namespace Refedle.App.Tui.Ui.Views;

/// <summary>
/// Manages viewport scrolling state for virtual grid rendering.
/// Tracks the visible row range and handles scrolling operations.
/// </summary>
internal sealed class ViewportState
{
    /// <summary>
    /// Gets or sets the zero-based index of the top visible row.
    /// </summary>
    public int TopRow { get; set; }

    /// <summary>
    /// Gets or sets the number of rows visible in the viewport.
    /// </summary>
    public int VisibleRowCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of rows in the dataset.
    /// </summary>
    public long TotalRowCount { get; set; }

    /// <summary>
    /// Gets the zero-based index of the bottom visible row.
    /// </summary>
    public int BottomRow => (int)Math.Min(TopRow + VisibleRowCount - 1, TotalRowCount - 1);

    /// <summary>
    /// Scrolls the viewport up by the specified number of rows.
    /// </summary>
    /// <param name="delta">The number of rows to scroll up.</param>
    public void ScrollUp(int delta)
    {
        TopRow = Math.Max(0, TopRow - delta);
    }

    /// <summary>
    /// Scrolls the viewport down by the specified number of rows.
    /// </summary>
    /// <param name="delta">The number of rows to scroll down.</param>
    public void ScrollDown(int delta)
    {
        var maxTopRow = (int)Math.Max(0, TotalRowCount - VisibleRowCount);
        TopRow = Math.Min(maxTopRow, TopRow + delta);
    }

    /// <summary>
    /// Scrolls the viewport up by one page (one viewport height).
    /// </summary>
    public void PageUp()
    {
        ScrollUp(VisibleRowCount);
    }

    /// <summary>
    /// Scrolls the viewport down by one page (one viewport height).
    /// </summary>
    public void PageDown()
    {
        ScrollDown(VisibleRowCount);
    }

    /// <summary>
    /// Jumps to the first row of the dataset.
    /// </summary>
    public void JumpToTop()
    {
        TopRow = 0;
    }

    /// <summary>
    /// Jumps to the last row of the dataset.
    /// </summary>
    public void JumpToBottom()
    {
        TopRow = (int)Math.Max(0, TotalRowCount - VisibleRowCount);
    }
}
