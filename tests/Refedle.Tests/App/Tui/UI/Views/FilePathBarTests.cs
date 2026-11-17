using AwesomeAssertions;
using Refedle.App.Tui.UI.Views;
using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;

namespace Refedle.Tests.App.Tui.UI.Views;

public sealed partial class FilePathBarTests
{
    // The bar starts at column 7 and fills the rest of the parent, so a parent
    // of width 30 gives the bar a visible width of 23 columns.
    private const int ParentWidth = 30;
    private const int BarWidth = 23;

    private static View CreateParent(int width) => new() { Width = width, Height = 3 };

    private static FilePathBar AddBar(View parent)
    {
        FilePathBar bar = new();
        parent.Add(bar);
        parent.Layout();
        return bar;
    }

    [Fact]
    public void Text_BeforeAnyPathIsSet_IsEmpty()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        string text = bar.Text;

        // Assert
        text.Should().BeEmpty();
    }

    [Fact]
    public void Layout_WhenAdded_StartsAfterFileMenuLabelOnFirstRow()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);

        // Act
        using FilePathBar bar = AddBar(parent);

        // Assert
        bar.Frame.X.Should().Be(7);
        bar.Frame.Y.Should().Be(0);
        bar.Frame.Height.Should().Be(1);
        bar.Viewport.Width.Should().Be(BarWidth);
    }

    [Fact]
    public void SetFilePath_WithPathShorterThanWidth_ShowsPathUnchanged()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/data/orders.csv");

        // Assert
        bar.Text.Should().Be("/data/orders.csv");
    }

    [Fact]
    public void SetFilePath_WithPathExactlyAsWideAsBar_ShowsPathUnchanged()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = new string('a', BarWidth - 4) + ".csv";

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.Should().Be(path);
    }

    [Fact]
    public void SetFilePath_WithPathWiderThanBar_ElidesLeadingPartAndKeepsFileNameSide()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/home/user/projects/data/customer_orders_2026.csv");

        // Assert
        bar.Text.Should().Be("…stomer_orders_2026.csv");
    }

    [Fact]
    public void SetFilePath_WithPathWiderThanBar_ShowsTextExactlyAsWideAsBar()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/home/user/projects/data/customer_orders_2026.csv");

        // Assert
        bar.Text.Should().HaveLength(BarWidth);
    }

    [Fact]
    public void SetFilePath_WithPathOneColumnWiderThanBar_ReplacesFirstTwoCharactersWithEllipsis()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = new string('a', BarWidth - 4) + "b.csv";

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.Should().Be("…" + path[2..]);
    }

    [Fact]
    public void SetFilePath_WithUnderscoresInPath_ShowsPathUnchanged()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/a_b/my_file.csv");

        // Assert
        bar.Text.Should().Be("/a_b/my_file.csv");
    }

    [Fact]
    public void SetFilePath_WithEmptyString_ClearsPreviouslyShownPath()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        bar.SetFilePath("/data/orders.csv");

        // Act
        bar.SetFilePath(string.Empty);

        // Assert
        bar.Text.Should().BeEmpty();
    }

    [Fact]
    public void SetFilePath_WhenCalledAgain_ReplacesPreviousPath()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        bar.SetFilePath("/data/first.csv");

        // Act
        bar.SetFilePath("/data/second.csv");

        // Assert
        bar.Text.Should().Be("/data/second.csv");
    }

    [Fact]
    public void Width_WhenParentIsNarrowed_ElidesPreviouslyFullyShownPath()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        bar.SetFilePath("/data/orders_2026.csv");

        // Act
        parent.Width = 20;
        parent.Layout();

        // Assert
        bar.Text.Should().Be("…ers_2026.csv");
    }

    [Fact]
    public void Width_WhenParentIsWidened_ShowsPreviouslyElidedPathInFull()
    {
        // Arrange
        using View parent = CreateParent(20);
        using FilePathBar bar = AddBar(parent);
        bar.SetFilePath("/data/orders_2026.csv");

        // Act
        parent.Width = ParentWidth;
        parent.Layout();

        // Assert
        bar.Text.Should().Be("/data/orders_2026.csv");
    }

    [Fact]
    public void SetFilePath_WithWideCjkPathThatFitsByCharCountButNotByColumns_ElidesToFitBarColumns()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = "/データ/顧客注文一覧/売上.csv"; // 20 chars, 30 columns

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.GetColumns().Should().BeLessThanOrEqualTo(BarWidth);
        bar.Text.Should().StartWith("…").And.EndWith("売上.csv");
    }

    [Fact]
    public void SetFilePath_WithWideCjkPathWhoseTailFitsExactly_FillsBarColumns()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = "/a/顧客注文一覧一覧一覧一覧.csv"; // tail after "…" has 22 columns available

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.Should().Be("…文一覧一覧一覧一覧.csv");
        bar.Text.GetColumns().Should().Be(BarWidth);
    }

    [Theory]
    [InlineData("/a/dir/😀😀😀😀😀😀😀😀😀😀😀😀.csv")]
    [InlineData("/a/dir/x😀😀😀😀😀😀😀😀😀😀😀😀.csv")]
    public void SetFilePath_WithEmojiNearElisionBoundary_FitsBarColumnsWithoutSplittingSurrogatePairs(string path)
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.GetColumns().Should().BeLessThanOrEqualTo(BarWidth);
        bar.Text.Should().EndWith(".csv");
        bar.Text.EnumerateRunes().Should().NotContain(r => r == System.Text.Rune.ReplacementChar);
        char.IsLowSurrogate(bar.Text[1]).Should().BeFalse();
    }

    [Fact]
    public void SetFilePath_WithPathWithinBarColumnsButContainingWideCharacters_ShowsPathUnchanged()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = "/顧客/注文.csv"; // 12 columns

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.Should().Be(path);
    }

    [Fact]
    public void SetFilePath_WithWidePathWiderThanBarByColumns_ShowsEllipsisAndFileNameSide()
    {
        // Arrange
        using View parent = CreateParent(18); // bar viewport width: 11 columns
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/a/日本語.csv"); // 10 chars, 13 columns

        // Assert
        bar.Text.Should().Be("…日本語.csv");
    }

    [Fact]
    public void SetFilePath_WithNonNormalizedRelativePath_ShowsInputUnchanged()
    {
        // Arrange
        using View parent = CreateParent(40);
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("./data/../data/orders.csv");

        // Assert
        bar.Text.Should().Be("./data/../data/orders.csv");
    }

    [Fact]
    public void SetFilePath_WithOverlongPathAndOneColumnBar_ShowsOnlyEllipsis()
    {
        // Arrange
        using View parent = CreateParent(8); // bar viewport width: 1 column
        using FilePathBar bar = AddBar(parent);

        // Act
        bar.SetFilePath("/data/orders.csv");

        // Assert
        bar.Text.Should().Be("…");
    }

    [Fact]
    public void SetFilePath_WithCombiningAccentNearElisionBoundary_KeepsBaseLetterAndAccentTogether()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        string path = "/a/dir/dir/dir/dir/dir/dir/e\u0301e\u0301e\u0301.csv";

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.GetColumns().Should().BeLessThanOrEqualTo(BarWidth);
        bar.Text.Should().StartWith("…");
        bar.Text[1].Should().NotBe('\u0301');
        bar.Text.Should().EndWith("e\u0301e\u0301e\u0301.csv");
    }

    [Fact]
    public void SetFilePath_WithZwjEmojiNearElisionBoundary_KeepsEmojiSequenceWholeAndWithinBarColumns()
    {
        // Arrange
        using View parent = CreateParent(ParentWidth);
        using FilePathBar bar = AddBar(parent);
        const string family = "👨\u200D👩\u200D👧";
        string path = "/a/" + string.Concat(Enumerable.Repeat(family, 12)) + ".csv"; // each sequence is 2 columns

        // Act
        bar.SetFilePath(path);

        // Assert
        bar.Text.GetColumns().Should().BeLessThanOrEqualTo(BarWidth);
        bar.Text.Should().Be("…" + string.Concat(Enumerable.Repeat(family, 9)) + ".csv");
    }

}
