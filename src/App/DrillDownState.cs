using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;
using Refedle.Engine.Models.Actions;

namespace Refedle.App;

/// <summary>
/// Holds the in-memory state produced by the DrillDown command.
/// </summary>
/// <param name="Rows">The extracted child rows rendered by the focused table.</param>
/// <param name="Schema">The schema inferred for the extracted rows.</param>
/// <param name="PreviousMode">The view mode to return to when leaving the DrillDown session.</param>
/// <param name="KeyPath">The file location the DrillDown was taken from.</param>
/// <param name="ActionStack">The morph actions applied within this DrillDown session.</param>
/// <param name="HasUnsavedChanges">
/// Whether <paramref name="ActionStack"/> contains changes not reflected in a recipe file.
/// Defaults to false: a freshly created session — empty, or replaying a just-loaded recipe — is
/// clean. Mutation sites must set it to true explicitly; a non-destructive <c>with</c> copy
/// carries over the previous value, which is only correct when the stack is untouched.
/// </param>
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema,
    ViewMode PreviousMode,
    IReadOnlyList<KeyPathSegment> KeyPath,
    IReadOnlyList<MorphAction> ActionStack,
    bool HasUnsavedChanges = false);
