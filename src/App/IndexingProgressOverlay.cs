using System.Globalization;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.App;

/// <summary>
/// Compact indexing progress overlay: a spinner and a one-line label placed one row above
/// the status bar at the right edge of the parent view.
/// Must be used on the UI thread; it adds and removes Terminal.Gui views.
/// </summary>
internal sealed class IndexingProgressOverlay : IDisposable
{
    private const string SchemeName = "IndexingOverlay";

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
            Text = " Indexing…",
            X = Pos.AnchorEnd() - 1,
            Y = Pos.AnchorEnd(2),
            SchemeName = SchemeName,
        };
        _spinner = new SpinnerView
        {
            X = Pos.Left(_label) - 1,
            Y = Pos.AnchorEnd(2),
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
    /// Updates the label with the percentage and byte progress. Does nothing when the overlay
    /// is not shown or <paramref name="fileSize"/> is not positive.
    /// </summary>
    public void Update(long bytesRead, long fileSize)
    {
        if (_label is null)
        {
            return;
        }

        if (fileSize <= 0)
        {
            return;
        }

        var fraction = (float)bytesRead / fileSize;
        _label.Text = string.Create(
            CultureInfo.InvariantCulture,
            $" Indexing… {fraction * 100:F0}%  {FormatBytes(bytesRead)} / {FormatBytes(fileSize)}");
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

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)GB:F1}GB"),
            >= MB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)MB:F1}MB"),
            >= KB => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)KB:F1}KB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes}B"),
        };
    }
}
