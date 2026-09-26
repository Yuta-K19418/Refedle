
namespace Refedle.App.Tui;

/// <summary>
/// Defines the available view modes in the TUI application.
/// </summary>
internal enum ViewMode
{
    /// <summary>
    /// File selection dialog view.
    /// </summary>
    FileSelection,

    /// <summary>
    /// CSV table view with virtualized grid rendering.
    /// </summary>
    CsvTable,

    /// <summary>
    /// JSON Lines tree view with hierarchical node display.
    /// </summary>
    JsonLinesTree,

    /// <summary>
    /// JSON Lines table view with virtualized grid rendering.
    /// </summary>
    JsonLinesTable,

    /// <summary>
    /// JSON Array tree view with hierarchical element display.
    /// </summary>
    JsonArrayTree,

    /// <summary>
    /// JSON Array table view with virtualized grid rendering.
    /// </summary>
    JsonArrayTable,

    /// <summary>
    /// JSON Object tree view with hierarchical key-value display.
    /// </summary>
    JsonObjectTree,

    /// <summary>
    /// Focused table view displaying the DrillDown result as rows.
    /// </summary>
    FocusedTable,

    /// <summary>
    /// Placeholder view displaying loaded file information.
    /// Will be replaced by CsvTable, JsonTable views in future issues.
    /// </summary>
    PlaceholderView,
}
