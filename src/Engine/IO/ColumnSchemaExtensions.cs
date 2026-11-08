using Refedle.Engine.Models;
using Refedle.Engine.Types;

namespace Refedle.Engine.IO;

/// <summary>
/// Extension methods for ColumnSchema to handle type updates with early-exit optimization.
/// </summary>
public static class ColumnSchemaExtensions
{
    /// <summary>
    /// Updates the column type if the observed type differs from current using Copy-on-Write pattern.
    /// </summary>
    /// <param name="schema">The column schema to update.</param>
    /// <param name="observedType">The newly observed type.</param>
    /// <returns>
    /// New ColumnSchema instance if type changed, otherwise returns the same instance.
    /// </returns>
    public static ColumnSchema WithUpdatedType(this ColumnSchema schema, ColumnType observedType)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Type == observedType)
        {
            return schema; // No change - return same instance
        }

        var resolvedType = ColumnTypeResolver.Resolve(schema.Type, observedType);

        // Copy-on-Write: return same instance if no change
        return schema.Type == resolvedType ? schema : (schema with { Type = resolvedType });
    }

    /// <summary>
    /// Marks the column as nullable if not already using Copy-on-Write pattern.
    /// </summary>
    /// <param name="schema">The column schema to update.</param>
    /// <returns>
    /// New ColumnSchema instance if nullable changed, otherwise returns the same instance.
    /// </returns>
    public static ColumnSchema WithMarkedNullable(this ColumnSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.IsNullable)
        {
            return schema; // Already nullable - return same instance
        }

        return schema with { IsNullable = true };
    }
}
