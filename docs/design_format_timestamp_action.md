# Design: FormatTimestampAction — Reformat Timestamp Column Values

## Overview

Add `FormatTimestampAction` as a new `MorphAction` subtype that reformats the string
representation of date/time values in a column already typed as `ColumnType.Timestamp`.
Users supply a required target format (a standard .NET date/time format string). The action
applies uniformly to CSV and JSON Lines formats via the existing `CellTransformSpec` /
`RecordProcessor` pipeline.

---

## Requirements

- Reformat every cell in a `Timestamp` column from its current string representation to a
  new format string.
- `TargetFormat` is required.
- Parsing always uses `DateTime.TryParse` with `CultureInfo.InvariantCulture`, consistent
  with `TypeInferrer`. Because a column is only typed as `Timestamp` after all its cells
  have passed that same parse check, parse failures at action-execution time are not
  expected.
- The column's `ColumnType` **must** be `Timestamp`; applying the action to any other type
  returns an error `Result` — no exception is thrown.
- Cells that cannot be parsed return an error `Result` — values are **not** silently dropped
  or replaced.
- The action is serialized into `Recipe.Actions` as all other `MorphAction` subtypes
  (JSON polymorphism via `System.Text.Json` source generators, AOT-safe).

---

## New Types

### `FormatTimestampAction` record

**File**: `src/Engine/Models/Actions/FormatTimestampAction.cs`

```csharp
namespace Refedle.Engine.Models.Actions;

public sealed record FormatTimestampAction : MorphAction
{
    public required string ColumnName { get; init; }

    /// <summary>
    /// .NET date/time format string that all values will be reformatted to.
    /// </summary>
    public required string TargetFormat { get; init; }

    public override string Description =>
        $"Format timestamp column '{ColumnName}' → \"{TargetFormat}\"";
}
```

Registration in `MorphAction.cs`:

```csharp
[JsonDerivedType(typeof(FormatTimestampAction), typeDiscriminator: "format_timestamp")]
```

### `TimestampFormatSpec` (new `CellTransformSpec` subtype)

**File**: `src/Engine/Models/CellTransformSpec.cs` — add alongside `FillSpec`

```csharp
/// <summary>
/// Reformats a Timestamp cell value to the specified target format string.
/// </summary>
public sealed record TimestampFormatSpec(string TargetFormat) : CellTransformSpec;
```

This activates the commented-out extension point already present in
`src/Engine/Models/CellTransformSpec.cs`.

---

## Changed Files

### New

| File | Purpose |
|------|---------|
| `src/Engine/Models/Actions/FormatTimestampAction.cs` | New record |
| `tests/Refedle.Tests/Engine/Models/Actions/FormatTimestampActionTests.cs` | Unit tests for the record |
| `tests/Refedle.Tests/Engine/ActionApplierTests.FormatTimestamp.cs` | `ActionApplier` tests for the new action |
| `tests/Refedle.Tests/App/Cli/RecordProcessorFormatTimestampTests.cs` | `RecordProcessor` transform tests |

### Modified

| File | Change |
|------|--------|
| `src/Engine/Models/Actions/MorphAction.cs` | Add `[JsonDerivedType(typeof(FormatTimestampAction), typeDiscriminator: "format_timestamp")]` |
| `src/Engine/Models/JsonContext.cs` | Add `[JsonSerializable(typeof(FormatTimestampAction))]` |
| `src/Engine/Models/CellTransformSpec.cs` | Uncomment / add `TimestampFormatSpec` subtype |
| `src/Engine/ActionApplier.cs` | Handle `FormatTimestampAction`; validate column type; emit `TimestampFormatSpec` |
| `src/App/Cli/Commands/Apply/RecordProcessor.cs` | Add `TimestampFormatSpec` arm to the transform switch |
| `src/Engine/Recipes/MorphActionParser.cs` | Add `"format_timestamp"` case |
| `src/Engine/Recipes/RecipeYamlSerializer.cs` | Add `FormatTimestampAction` case |

---

## Implementation Logic

### `ActionApplier` changes

When a `FormatTimestampAction` is encountered during the action-stack walk:

1. Look up `ColumnName` in `nameToWorkingIndex`. If not found, skip silently (column was
   deleted earlier — consistent with all other actions).
2. Check the working column's `ColumnType` (i.e., `workingColumns[idx].Type`, which reflects
   any preceding `CastColumnAction`s). If it is **not** `ColumnType.Timestamp`,
   return `Results.Failure<BatchOutputSchema>` with a descriptive message:
   `"FormatTimestampAction requires column '{ColumnName}' to be of type Timestamp, but it is {actualType}."`.
3. If the type is valid, record
   `workingIndex → new TimestampFormatSpec(action.TargetFormat)`
   in `transformsByWorkingIndex` (last write wins, consistent with `FillColumnAction`).

Because `BuildOutputSchema` can now fail (type mismatch), its return type changes from
`BatchOutputSchema` to `Result<BatchOutputSchema>`. All callers — `Runner.RunAsync` and
any tests — must be updated accordingly.

### `RecordProcessor` changes

Add a new arm to the transform switch in `ProcessAsync`:

```csharp
TimestampFormatSpec fmt =>
    ApplyTimestampFormat(reader.GetCellSpan(i), fmt).AsSpan(),
```

The helper `ApplyTimestampFormat` (private static):

```csharp
private static string ApplyTimestampFormat(ReadOnlySpan<char> raw, TimestampFormatSpec fmt)
{
    if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
        throw new FormatException($"Could not parse timestamp value '{raw}'.");
    }

    return parsed.ToString(fmt.TargetFormat, CultureInfo.InvariantCulture);
}
```

> **Error surface**: `FormatException` propagates up through `ProcessAsync` and is caught
> by `Runner.RunAsync`, which maps it to `ExitCode.Failure` with an error message — the
> same path already used for I/O errors. This avoids threading `Result<T>` through the
> hot per-cell loop.

### `MorphActionParser` changes

Add a new parse case for `"format_timestamp"`:

```csharp
"format_timestamp" => ParseFormatTimestampAction(fields),
```

```csharp
private static Result<MorphAction> ParseFormatTimestampAction(Dictionary<string, string> fields)
{
    if (!fields.TryGetValue("columnName", out var columnName))
        return Results.Failure<MorphAction>("Missing required field 'columnName' for format_timestamp action");

    if (!fields.TryGetValue("targetFormat", out var targetFormat))
        return Results.Failure<MorphAction>("Missing required field 'targetFormat' for format_timestamp action");

    return Results.Success<MorphAction>(new FormatTimestampAction
    {
        ColumnName = columnName,
        TargetFormat = targetFormat,
    });
}
```

---

## `ActionApplier` Return Type Change

`BuildOutputSchema` is updated from:

```csharp
public static BatchOutputSchema BuildOutputSchema(TableSchema schema, IReadOnlyList<MorphAction> actions)
```

to:

```csharp
public static Result<BatchOutputSchema> BuildOutputSchema(TableSchema schema, IReadOnlyList<MorphAction> actions)
```

This is a breaking change for all callers. Known call sites:

| Caller | Required change |
|--------|----------------|
| `src/App/Cli/Commands/Apply/Runner.cs` | Unwrap `Result`; propagate failure to `ExitCode.Failure` |
| `tests/Refedle.Tests/Engine/ActionApplierTests.cs` | All existing tests unwrap `.Value` or assert on `Result.IsSuccess` |

---

## Test Cases

### `FormatTimestampActionTests`

- `Description` property returns correct string.
- JSON round-trip preserves `ColumnName`, `TargetFormat`, and type discriminator `"format_timestamp"`.

### `ActionApplierTests` additions

- `FormatTimestampAction` on a `Timestamp` column → `TimestampFormatSpec` attached to output column.
- `FormatTimestampAction` on a `Text` column → `Result.IsSuccess == false`, error message
  includes column name and actual type.
- `FormatTimestampAction` on a deleted column → silently skipped, result is success.

### `RecordProcessorFormatTimestampTests`

- ISO 8601 source → custom `TargetFormat` → correct output.
- One datetime format → different `TargetFormat` → correct output.
- Unparseable cell → `FormatException` propagated.
- Empty dataset → only header written, no exception.
- Multi-column recipe where only the Timestamp column has a `TimestampFormatSpec` →
  other columns pass through unchanged.

---

## Decision Record

### Rationale

`TimestampFormatSpec` follows the extension point already sketched (and commented out) in
`CellTransformSpec.cs` during the `FillColumnAction` design. Activating it here keeps the
`RecordProcessor` hot loop pattern identical — a single `switch` on `CellTransformSpec` —
and avoids introducing a parallel execution path.

Placing the type-validity check in `ActionApplier` (at schema-build time) rather than in
`RecordProcessor` (at cell-processing time) means the error is surfaced before any I/O
occurs. This is consistent with the principle that `ActionApplier` is the authoritative
validator of the action stack.

Returning `Result<BatchOutputSchema>` from `BuildOutputSchema` (rather than throwing) keeps
the Engine layer exception-free, matching the existing `Result<T>` pattern used throughout
the codebase.

### Alternatives Considered

**Include a `SourceFormat` property for explicit parse control**: Rejected because
`TypeInferrer` already verifies every cell in a `Timestamp` column is parseable via
`DateTime.TryParse + InvariantCulture`. Adding `SourceFormat` / `TryParseExact` would
introduce complexity without benefit for data produced by the app itself.

**Parse and format inside `ActionApplier`, producing pre-formatted strings in a lookup
table**: Rejected because it requires loading all values into memory before streaming begins,
breaking the zero-allocation streaming invariant for large files.

**Keep `BuildOutputSchema` returning `BatchOutputSchema` and throw on type mismatch**:
Rejected because throwing from a pure, stateless utility method is inconsistent with the
`Result<T>` pattern used for all other validation failures in the Engine layer.

**Validate column type at UI layer (before adding the action to the stack)**: Rejected
because the Engine should be the single source of truth for validation; UI-only guards
would be bypassed by the headless CLI path.

### Consequences

- `BuildOutputSchema`'s return type changes to `Result<BatchOutputSchema>`, requiring
  updates to all call sites. The scope is small (one production caller, one test file).
- Future `CellTransformSpec` subtypes (e.g., a numeric rounding spec) follow the same
  pattern: add a subtype, handle it in `ActionApplier`, add an arm in `RecordProcessor`.
  No structural changes needed.
- `CultureInfo.InvariantCulture` is used throughout for both parsing and formatting,
  consistent with `TypeInferrer`.
