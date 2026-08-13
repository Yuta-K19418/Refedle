namespace Refedle.Engine.Models.Actions;

/// <summary>
/// Declares how a <see cref="FilterAction"/>'s value should be interpreted during evaluation.
/// Distinct from <c>ColumnType</c>: scoped to filter comparison only, with the three values
/// meaningful for that purpose.
/// </summary>
public enum ComparisonType
{
    /// <summary>Interpret the value as a string.</summary>
    Text,

    /// <summary>Interpret the value as a number.</summary>
    Number,

    /// <summary>Interpret the value as a timestamp.</summary>
    Timestamp,
}
