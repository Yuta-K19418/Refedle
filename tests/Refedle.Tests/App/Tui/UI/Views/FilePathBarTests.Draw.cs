using System.Text;
using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Refedle.Tests.App.Tui.UI.Views;

// Draw coverage: renders the bar through a real ANSI driver and asserts the captured screen
// cells, complementing the Text-level tests in FilePathBarTests.cs.
public sealed partial class FilePathBarTests
{
    // Fails the test clearly when the ANSI driver has not been initialized.
    private static IDriver DriverOf(IApplication app) =>
        app.Driver ?? throw new InvalidOperationException("the ANSI driver is not initialized");

    // Fails the test clearly when the driver has not captured the drawn screen.
    private static Cell[,] ScreenContents(IDriver driver) =>
        driver.Contents ?? throw new InvalidOperationException("the driver captured no screen contents");

    // Fails the test clearly when a captured screen cell carries no attribute.
    private static Attribute CellAttribute(Cell[,] contents, int row, int col) =>
        contents[row, col].Attribute
            ?? throw new InvalidOperationException($"screen cell ({row}, {col}) carries no attribute");

    // Extracts the attributes of the first columns of one captured screen row so tests can
    // assert on cells without null-suppression.
    private static Attribute[] ScreenRowAttributes(IApplication app, int row, int columns)
    {
        IDriver driver = DriverOf(app);
        Cell[,] contents = ScreenContents(driver);
        return [.. Enumerable.Range(0, columns)
            .Select(col => CellAttribute(contents, row, col))];
    }

    // Reconstructs the text of the first columns of one captured screen row.
    private static string ScreenRowText(IApplication app, int row, int columns)
    {
        IDriver driver = DriverOf(app);
        Cell[,] contents = ScreenContents(driver);
        StringBuilder text = new();
        for (int col = 0; col < columns; col++)
        {
            text.Append(contents[row, col].Grapheme);
        }

        return text.ToString();
    }

    [Fact]
    public void Draw_WithPathShorterThanBar_PaintsFullBarWidthWithMenuColors()
    {
        // Arrange — a Window works as the top-level for Begin; no border so the bar's geometry
        // matches the plain View used by the text tests.
        using IApplication app = TestAppFactory.Create();
        using Window parent = new() { Width = ParentWidth, Height = 3, BorderStyle = LineStyle.None };
        using MenuBarItem fileMenuItem = new("_File", []);
        using MenuBar menuBar = new() { Menus = [fileMenuItem] };
        parent.Add(menuBar);
        using FilePathBar bar = AddBar(parent);
        bar.SetMenuAttribute(() => fileMenuItem.GetAttributeForRole(VisualRole.Normal));
        bar.SetFilePath("/data/orders.csv");
        app.StopAfterFirstIteration = true;
        app.Begin(parent);

        // Act
        app.LayoutAndDraw();

        // Assert — label and bar must read as one continuous bar through the parent's right edge.
        // The label's hotkey letter carries a different foreground/style, so only the background
        // is asserted over the label, while the bar columns check the full menu attribute.
        Attribute menuAttribute = fileMenuItem.GetAttributeForRole(VisualRole.Normal);
        Attribute[] attributes = ScreenRowAttributes(app, 0, ParentWidth);
        attributes.Select(attribute => attribute.Background)
            .Should().OnlyContain(background => background == menuAttribute.Background);
        attributes.Skip(7)
            .Should().OnlyContain(attribute => attribute == menuAttribute);
        ScreenRowText(app, 0, ParentWidth)[7..]
            .Should().Be("/data/orders.csv".PadRight(ParentWidth - 7));
    }

    [Fact]
    public void Draw_WithNoPath_PaintsOnlyFileLabelWithMenuColors()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using Window parent = new() { Width = ParentWidth, Height = 3, BorderStyle = LineStyle.None };
        using MenuBarItem fileMenuItem = new("_File", []);
        using MenuBar menuBar = new() { Menus = [fileMenuItem] };
        parent.Add(menuBar);
        using FilePathBar bar = AddBar(parent);
        app.StopAfterFirstIteration = true;
        app.Begin(parent);

        // Act
        app.LayoutAndDraw();

        // Assert — without a path nothing may be painted after the File label: the bar columns
        // must hold the parent Window's own Normal attribute.
        Attribute[] attributes = ScreenRowAttributes(app, 0, ParentWidth);
        attributes[0].Background.Should().Be(fileMenuItem.GetAttributeForRole(VisualRole.Normal).Background);
        attributes.Skip(7)
            .Should().OnlyContain(attribute => attribute == parent.GetAttributeForRole(VisualRole.Normal));
    }

    [Fact]
    public void Draw_WithPathExactlyAsWideAsBar_PaintsWholeRowUpToBarEndWithMenuColors()
    {
        // Arrange
        using IApplication app = TestAppFactory.Create();
        using Window parent = new() { Width = ParentWidth, Height = 3, BorderStyle = LineStyle.None };
        using MenuBarItem fileMenuItem = new("_File", []);
        using MenuBar menuBar = new() { Menus = [fileMenuItem] };
        parent.Add(menuBar);
        using FilePathBar bar = AddBar(parent);
        bar.SetMenuAttribute(() => fileMenuItem.GetAttributeForRole(VisualRole.Normal));
        string path = new string('a', BarWidth - 4) + ".csv"; // exactly BarWidth columns, no elision needed
        bar.SetFilePath(path);
        app.StopAfterFirstIteration = true;
        app.Begin(parent);

        // Act
        app.LayoutAndDraw();

        // Assert — the bar fills to the parent's right edge and stays continuous with the File label.
        Attribute menuAttribute = fileMenuItem.GetAttributeForRole(VisualRole.Normal);
        Attribute[] attributes = ScreenRowAttributes(app, 0, ParentWidth);
        attributes.Select(attribute => attribute.Background)
            .Should().OnlyContain(background => background == menuAttribute.Background);
        attributes.Skip(7)
            .Should().OnlyContain(attribute => attribute == menuAttribute);
        ScreenRowText(app, 0, ParentWidth)[7..].Should().Be(path);
    }

    [Fact]
    public void Draw_AfterClearingShownPath_PaintsOnlyFileLabel()
    {
        // Arrange — draw the path once, then clear it, so the test exercises the repaint
        // transition rather than the initial no-path state.
        using IApplication app = TestAppFactory.Create();
        using Window parent = new() { Width = ParentWidth, Height = 3, BorderStyle = LineStyle.None };
        using MenuBarItem fileMenuItem = new("_File", []);
        using MenuBar menuBar = new() { Menus = [fileMenuItem] };
        parent.Add(menuBar);
        using FilePathBar bar = AddBar(parent);
        bar.SetMenuAttribute(() => fileMenuItem.GetAttributeForRole(VisualRole.Normal));
        bar.SetFilePath("/data/orders.csv");
        app.StopAfterFirstIteration = true;
        app.Begin(parent);
        app.LayoutAndDraw();

        // Act
        bar.SetFilePath(string.Empty);
        app.LayoutAndDraw();

        // Assert — clearing must wipe the bar: the columns right of the File label must hold
        // the parent Window's own Normal attribute and contain no path characters.
        Attribute[] attributes = ScreenRowAttributes(app, 0, ParentWidth);
        attributes.Skip(7)
            .Should().OnlyContain(attribute => attribute == parent.GetAttributeForRole(VisualRole.Normal));
        ScreenRowText(app, 0, ParentWidth)[7..].Should().Be(new string(' ', ParentWidth - 7));
    }
}
