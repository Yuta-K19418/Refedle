using System.Globalization;

namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Resolves a KeyPath to a single destination node, for replaying a recipe's recorded DrillDown
/// location. Unlike <see cref="KeyPathTraverser.ExtractRows"/>, each segment has exactly one
/// destination, so this is a plain loop, not a DFS. Public so the App-layer recipe loader can call
/// <see cref="ResolveSingleNode"/> directly.
/// </summary>
public static class KeyPathNodeResolver
{
    /// <summary>
    /// Resolves <paramref name="remainingKeyPath"/> to a single destination node starting from
    /// <paramref name="startBytes"/>. Returns a failure — rather than
    /// <see cref="KeyPathTraverser.ExtractRows"/>'s silent-skip semantics — when the recorded path no
    /// longer resolves, since a recipe load must surface an explicit error (e.g. the underlying file
    /// changed) instead of silently producing an empty DrillDown.
    /// </summary>
    /// <param name="startBytes">The bytes to begin resolution from.</param>
    /// <param name="remainingKeyPath">The path segments still to resolve.</param>
    /// <returns>The resolved node's bytes, or a failure describing why the path could not be resolved.</returns>
    public static Result<JsonRawBytes> ResolveSingleNode(
        JsonRawBytes startBytes, IReadOnlyList<KeyPathSegment> remainingKeyPath)
    {
        ArgumentNullException.ThrowIfNull(remainingKeyPath);

        var currentBytes = startBytes;
        foreach (var segment in remainingKeyPath)
        {
            var nextResult = segment.Kind == KeyPathSegmentKind.Key
                ? ResolveKeySegment(currentBytes, segment)
                : ResolveIndexSegment(currentBytes, segment);

            if (nextResult.IsFailure)
            {
                return nextResult;
            }

            currentBytes = nextResult.Value;
        }

        return Results.Success(currentBytes);
    }

    private static Result<JsonRawBytes> ResolveKeySegment(JsonRawBytes currentBytes, KeyPathSegment segment)
    {
        var valueBytes = KeyPathLeafCollector.FindValueByKey(currentBytes, segment.Value);
        return valueBytes is { } bytes
            ? Results.Success(bytes)
            : Results.Failure<JsonRawBytes>($"DrillDown path key \"{segment.Value}\" was not found.");
    }

    private static Result<JsonRawBytes> ResolveIndexSegment(JsonRawBytes currentBytes, KeyPathSegment segment)
    {
        // Index-kind KeyPathSegment.Value is normally always in "[N]" form (see KeyPathSegment's doc
        // comment), but a recipe is user-editable YAML, so a malformed label must fail cleanly here
        // rather than slice out of bounds or silently misparse.
        if (segment.Value is not { Length: >= 2 } value || value[0] != '[' || value[^1] != ']')
        {
            return Results.Failure<JsonRawBytes>($"DrillDown path index \"{segment.Value}\" is not in \"[N]\" form.");
        }

        if (!int.TryParse(
            value.AsSpan(1, value.Length - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return Results.Failure<JsonRawBytes>($"DrillDown path index \"{segment.Value}\" is not a valid integer.");
        }

        var elementBytes = KeyPathLeafCollector.FindArrayElementByIndex(currentBytes, index);
        return elementBytes is { } bytes
            ? Results.Success(bytes)
            : Results.Failure<JsonRawBytes>(
                string.Create(CultureInfo.InvariantCulture, $"DrillDown path index {index} could not be resolved (not an array, or out of range)."));
    }
}
