namespace Refedle.Engine.Recipes;

// DrillDownKeyPathPresent distinguishes "absent" (null) from "present but empty" ([]) —
// different, meaningful recipe states (see KeyPathTraverser.LastKeySegment). Parser-wide
// state, consumed by both section parsers, not root-only.
internal sealed record RecipeYamlParseState(
    string Name,
    string? Description,
    DateTimeOffset? LastModified,
    ParseState ParseState,
    bool DrillDownKeyPathPresent);
