using System.Globalization;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.App.Views;

/// <summary>
/// A single-row overlay on the MenuBar row (row 0) that shows the path of the currently
/// opened file right after the "File" menu label. Starts to the right of the label so the
/// MenuBar itself stays visible and clickable. When the path does not fit, its leading part
/// is elided so the file name stays visible. Display-only.
/// </summary>
internal sealed class FilePathBar : View
{
    // Fixed width matching the MenuBar's "File" label (" File " plus a separating column).
    // Starting the overlay past it keeps the label uncovered. If the label text or the
    // MenuBar layout changes, this value must be updated to match.
    private const int MenuBarMenuWidth = 7;
    private const string Ellipsis = "…";

    private string _path = string.Empty;
    private Func<Attribute>? _menuAttribute;

    internal FilePathBar()
    {
        X = MenuBarMenuWidth;
        Y = 0; // Same row as the MenuBar
        Width = Dim.Fill();
        Height = 1;
        HotKeySpecifier = new Rune('\uffff'); // Paths contain '_', which must not act as a hotkey marker.
        ViewportChanged += (_, _) => UpdateText();
    }

    /// <summary>
    /// Supplies the colors the path is painted with, so the path reads as one bar with the
    /// File menu item; callers typically pass the menu item's attribute for
    /// <see cref="VisualRole.Normal"/>.
    /// </summary>
    internal void SetMenuAttribute(Func<Attribute> menuAttribute)
    {
        _menuAttribute = menuAttribute;
        SetNeedsDraw();
    }

    /// <summary>
    /// Shows <paramref name="path"/> after the File menu label.
    /// Pass an empty string to clear the display.
    /// </summary>
    /// <param name="path">The full path of the opened file, or empty when none is open.</param>
    internal void SetFilePath(string path)
    {
        _path = path;
        UpdateText();
    }

    private void UpdateText()
    {
        var width = Viewport.Width;
        if (width <= 0 || _path.GetColumns() <= width)
        {
            Text = _path;
            return;
        }

        Text = width == 1
            ? Ellipsis
            : Ellipsis + TakeTailThatFits(_path, width - Ellipsis.GetColumns());
    }

    // Returning true skips the default Text draw, which would repaint the text with the view's
    // Normal role. Padding with spaces extends the menu colors over the slack so the label and
    // the path read as one bar to the viewport's right edge.
    protected override bool OnDrawingText(DrawContext? context)
    {
        if (Text.Length == 0 || _menuAttribute is null)
        {
            return true;
        }

        SetAttribute(_menuAttribute());
        var width = Viewport.Width;
        var col = 0;
        foreach (var rune in Text.EnumerateRunes())
        {
            var columns = rune.GetColumns();
            if (col + columns > width)
            {
                break;
            }

            AddRune(col, 0, rune);
            col += columns;
        }

        while (col < width)
        {
            AddRune(col, 0, (Rune)' ');
            col++;
        }

        return true;
    }

    // Takes the longest suffix of text whose display width is at most maxColumns, cutting
    // only on text-element (grapheme) boundaries so surrogate pairs are never split.
    private static string TakeTailThatFits(string text, int maxColumns)
    {
        var elementStarts = StringInfo.ParseCombiningCharacters(text);
        var usedColumns = 0;
        var tailStart = text.Length;

        for (var i = elementStarts.Length - 1; i >= 0; i--)
        {
            var elementEnd = i + 1 < elementStarts.Length ? elementStarts[i + 1] : text.Length;
            var element = text[elementStarts[i]..elementEnd];
            usedColumns += element.GetColumns();
            if (usedColumns > maxColumns)
            {
                break;
            }

            tailStart = elementStarts[i];
        }

        return text[tailStart..];
    }
}
