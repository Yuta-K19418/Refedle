using System.Globalization;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.App.Tui.UI;

/// <summary>
/// Compact filter-scan progress overlay: a spinner and a percentage label
/// placed at the right edge, on the bottom row of the parent view. The parent must be a
/// container whose content size is its viewport (e.g. the content container), never a
/// scrollable view whose content follows its row count.
/// Must be used on the UI thread; it adds and removes Terminal.Gui views.
/// </summary>
internal sealed class FilterScanProgressOverlay : IDisposable
{
    private const string SchemeName = "FilterScanOverlay";

    private View? _parent;
    private SpinnerView? _spinner;
    private Label? _label;

    /// <summary>
    /// Shows the overlay in <paramref name="parent"/>, replacing any overlay already shown.
    /// </summary>
    public void Show(View parent)
    {
        Dismiss();
        EnsureSchemeRegistered();
        _label = new Label
        {
            // Leading space keeps a gap between the spinner and the text inside the colored strip
            Text = " Filtering… 0%",
            X = Pos.AnchorEnd() - 1,
            Y = Pos.AnchorEnd(1),
            SchemeName = SchemeName,
        };
        _spinner = new SpinnerView
        {
            X = Pos.Left(_label) - 1,
            Y = Pos.AnchorEnd(1),
            Width = 1,
            Height = 1,
            AutoSpin = true,
            Style = new SpinnerStyle.Dots(),
            SchemeName = SchemeName,
        };

        _parent = parent;
        parent.Add(_label, _spinner);
    }

    /// <summary>
    /// Moves the overlay views above the parent's other subviews so a view added after
    /// <see cref="Show"/> cannot draw over them. Does nothing when the overlay is not shown.
    /// </summary>
    public void BringToFront()
    {
        if (_parent is null || _label is null || _spinner is null)
        {
            return;
        }

        _parent.MoveSubViewToEnd(_label);
        _parent.MoveSubViewToEnd(_spinner);
    }

    /// <summary>
    /// Advances the percentage label to the given scan position.
    /// Does nothing when the overlay is not shown.
    /// </summary>
    public void Update(int scannedRows, int totalRows)
    {
        if (_label is null)
        {
            return;
        }

        var fraction = totalRows > 0 ? Math.Clamp(scannedRows / (float)totalRows, 0f, 1f) : 0f;
        _label.Text = string.Create(
            CultureInfo.InvariantCulture,
            $" Filtering… {fraction * 100:F0}%");
    }

    /// <summary>
    /// Removes and disposes the overlay views if they are shown.
    /// </summary>
    public void Dismiss()
    {
        if (_spinner is not null)
        {
            _parent?.Remove(_spinner);
            _spinner.Dispose();
            _spinner = null;
        }

        if (_label is not null)
        {
            _parent?.Remove(_label);
            _label.Dispose();
            _label = null;
        }

        _parent = null;
    }

    public void Dispose() => Dismiss();

    private static void EnsureSchemeRegistered()
    {
        if (SchemeManager.TryGetScheme(SchemeName, out _))
        {
            return;
        }

        // DarkGray background distinguishes the overlay from the TableView underneath
        var attribute = new Attribute(ColorName16.White, ColorName16.DarkGray);
        SchemeManager.AddScheme(SchemeName, new Scheme
        {
            Normal = attribute,
            HotNormal = attribute,
            Focus = attribute,
            HotFocus = attribute,
        });
    }
}
