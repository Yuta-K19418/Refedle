using Refedle.Engine.Types;

namespace Refedle.App.Tui;

/// <summary>Base type for DrillDown command requests.</summary>
/// <param name="Format">Source file format.</param>
internal closed record DrillDownRequest(DataFormat Format);
