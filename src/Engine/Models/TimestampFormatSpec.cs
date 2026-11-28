namespace Refedle.Engine.Models;

/// <summary>
/// Reformats a Timestamp cell value to the specified target format string.
/// </summary>
public sealed record TimestampFormatSpec(string TargetFormat) : CellTransformSpec;
