using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.App;

/// <summary>Base type for DrillDown command requests.</summary>
/// <param name="Format">Source file format.</param>
internal abstract record DrillDownRequest(DataFormat Format);

/// <summary>Single-node DrillDown: JSON Object format, operates on the selected node only.</summary>
/// <param name="Format">Source file format.</param>
/// <param name="NodeBytes">Raw bytes of the selected node's JSON value.</param>
/// <param name="KeyPath">Ordered path segments from root to the selected node.</param>
/// <param name="InitialActionStack">
/// Actions to seed the resulting <see cref="DrillDownState"/> with, so a recipe-load replay lands
/// on the DrillDown already carrying its recorded actions. Empty for an interactive DrillDown.
/// </param>
internal sealed record SingleDrillDownRequest(
    DataFormat Format,
    JsonRawBytes NodeBytes,
    IReadOnlyList<KeyPathSegment> KeyPath,
    IReadOnlyList<MorphAction> InitialActionStack)
    : DrillDownRequest(Format);

/// <summary>Full-aggregation DrillDown: JSON Lines / JSON Array format, scans the entire file.</summary>
/// <param name="Format">Source file format.</param>
/// <param name="KeyPath">Ordered path segments from root to the selected node.</param>
/// <param name="InitialActionStack">
/// Actions to seed the resulting <see cref="DrillDownState"/> with, so a recipe-load replay lands
/// on the DrillDown already carrying its recorded actions. Empty for an interactive DrillDown.
/// </param>
internal sealed record FullAggregationDrillDownRequest(
    DataFormat Format,
    IReadOnlyList<KeyPathSegment> KeyPath,
    IReadOnlyList<MorphAction> InitialActionStack)
    : DrillDownRequest(Format);
