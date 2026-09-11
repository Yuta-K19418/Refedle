namespace Refedle.Engine.Models;

/// <summary>
/// Describes a runtime value transformation applied to a single output column cell.
/// Sealed subtypes are handled via pattern matching in CellTransformFormatter.
/// </summary>
public abstract record CellTransformSpec;

/// <summary>Replace every cell value with a fixed constant string.</summary>
public sealed record FillSpec(string Value) : CellTransformSpec;

/// <summary>
/// Reformats a Timestamp cell value to the specified target format string.
/// </summary>
public sealed record TimestampFormatSpec(string TargetFormat) : CellTransformSpec;
