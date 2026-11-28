namespace Refedle.Engine.Models;

/// <summary>
/// Describes a runtime value transformation applied to a single output column cell.
/// Sealed subtypes are handled via pattern matching in CellTransformFormatter.
/// </summary>
public closed record CellTransformSpec;
