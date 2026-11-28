namespace Refedle.Engine.Models;

/// <summary>Replace every cell value with a fixed constant string.</summary>
public sealed record FillSpec(string Value) : CellTransformSpec;
