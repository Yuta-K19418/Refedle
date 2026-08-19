using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Refedle.App.Views.Dialogs;

/// <summary>
/// Modal dialog displaying application key bindings and help information.
/// </summary>
internal sealed class HelpDialog : Dialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HelpDialog"/> class.
    /// </summary>
    internal HelpDialog()
    {
        Title = "Help - Key Bindings";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Absolute(54);
        Height = Dim.Absolute(37);

        var helpText = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            Text = HelpText,
        };

        Add(helpText);

        var closeButton = new Button { Text = "Close", IsDefault = true };
        closeButton.Accepting += (s, e) => App?.RequestStop();
        AddButton(closeButton);
    }

    private const string HelpText = """
        Global / File Operations
        -------------------------
        o         : Open File
        s         : Save Recipe
        q         : Quit
        t         : Toggle Tree/Table View (JSON Lines)
        x         : Context-Sensitive Action Menu
        c         : Clear all actions from the stack
        ?         : Help (this overlay)
        BackSpace : Return to originating tree view
                    (FocusedTable only)

        Navigation
        ----------
        h/j/k/l   : Move Left/Down/Up/Right
        gg        : Jump to first row
        G         : Jump to last row
        d/u       : Page Down/Up
        Enter     : Expand/Collapse (Tree View)

        Context Actions (via 'x' menu)
        ------------------------------
        Rename    : Rename the current column
        Delete    : Remove the current column
        Cast      : Change column data type
        Filter    : Add a filter based on current column
        Fill      : Fill empty cells in column
        Format    : Format timestamp columns
        DrillDown : Drill into the selected node
        """;

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        var baseKey = (char)(key.KeyCode & KeyCode.CharMask);
        var baseKeyLower = char.ToLowerInvariant(baseKey);

        if (key.KeyCode == KeyCode.Esc || baseKeyLower == 'q' || baseKeyLower == '?')
        {
            App?.RequestStop();
            return true;
        }

        return false;
    }
}
