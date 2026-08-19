# Design: FillColumnAction

## Requirements

Add a `FillColumnAction` that overwrites every value in a named column with a fixed string. Applies uniformly to CSV and JSON Lines formats. Use case: anonymization, masking, bulk initialization.

## Changed Files

### New

- `src/Engine/Models/Actions/FillColumnAction.cs` — new record
- `src/Engine/Models/CellTransformSpec.cs` — abstract base `CellTransformSpec` + `FillSpec` subtype (extension point for future `TimestampFormatSpec`)

### Modified

| File | Change |
|------|--------|
| `src/Engine/Models/Actions/MorphAction.cs` | Add `[JsonDerivedType(typeof(FillColumnAction), typeDiscriminator: "fill")]` |
| `src/Engine/Models/JsonContext.cs` | Add `[JsonSerializable(typeof(FillColumnAction))]` |
| `src/Engine/BatchOutputSchema.cs` | Add `CellTransformSpec?` to `BatchOutputColumn` |
| `src/Engine/ActionApplier.cs` | Handle `FillColumnAction`; set `Transform = new FillSpec(value)` on the output column |
| `src/App/Cli/RecordProcessor.cs` | Change `outputColumnCount: int` → `columns: IReadOnlyList<BatchOutputColumn>`; apply transform per cell |
| `src/Engine/Recipes/MorphActionParser.cs` | Add `"Fill"` case |
| `src/Engine/Recipes/RecipeYamlSerializer.cs` | Add `FillColumnAction` case |
| `src/Generators/FormatDispatcherGenerator.cs` | Pass `outputSchema.Columns` instead of `outputSchema.Columns.Count` to `RecordProcessor.ProcessAsync` |

### Tests

| File | Tests added |
|------|-------------|
| `tests/.../Engine/Models/Actions/FillColumnActionTests.cs` | NEW — Description property, JSON round-trip, type discriminator |
| `tests/.../Engine/ActionApplierTests.cs` | Fill: single column, non-existent column skipped, multi-action recipe with fill, empty dataset |
| `tests/.../Engine/Recipes/MorphActionParserTests.cs` | Parse fill: success, missing columnName, missing value, unknown type still fails |
| `tests/.../Engine/Recipes/RecipeYamlSerializerTests.cs` | Serialize fill action round-trip |
| `tests/.../App/Cli/RecordProcessorTests.cs` | Fill: single column, multi-column recipe with fill, empty dataset |

## Implementation Logic

### CellTransformSpec hierarchy (new file: `src/Engine/Models/CellTransformSpec.cs`)

Abstract base + subtypes following the same pattern as `MorphAction`:

```csharp
/// <summary>
/// Describes a runtime value transformation applied to a single output column cell.
/// Sealed subtypes are handled via pattern matching in RecordProcessor.
/// </summary>
public abstract record CellTransformSpec;

/// <summary>Replace every cell value with a fixed constant string.</summary>
public sealed record FillSpec(string Value) : CellTransformSpec;

// Defined here as an extension point; logic to be implemented separately
// public sealed record TimestampFormatSpec(string? SourceFormat, string TargetFormat) : CellTransformSpec;
```

### BatchOutputColumn (in `BatchOutputSchema.cs`)

`CellTransformSpec?` is added as an optional property (no constructor signature change on `BatchOutputSchema`):

```csharp
public sealed record BatchOutputColumn(
    string SourceName,
    string OutputName,
    CellTransformSpec? Transform = null);  // null = pass-through
```

`BatchOutputSchema` itself is **unchanged**.

### ActionApplier

During the action loop, when a `FillColumnAction` is encountered:
1. Look up the working column by `action.ColumnName` in `nameToWorkingIndex`. If not found, skip silently (consistent with other actions).
2. Record `workingIndex → new FillSpec(action.Value)` in a local `Dictionary<int, CellTransformSpec> transformsByWorkingIndex`.

When building `outputColumns`, attach the transform if present:

```csharp
List<BatchOutputColumn> outputColumns = [];
foreach (var kvp in nameToWorkingIndex.OrderBy(kvp => kvp.Value))
{
    var (name, _, _, outputName) = workingColumns[kvp.Value];
    var transform = transformsByWorkingIndex.GetValueOrDefault(kvp.Value);
    outputColumns.Add(new BatchOutputColumn(SourceName: name, OutputName: outputName, Transform: transform));
}
```

> **Note**: Multiple `FillColumnAction`s on the same column overwrite each other (last one wins), which is the natural dictionary behaviour.

### RecordProcessor

Signature change: `outputColumnCount: int` → `columns: IReadOnlyList<BatchOutputColumn>`:

```csharp
public static async ValueTask<ExitCode> ProcessAsync<TReader, TWriter>(
    TReader reader,
    TWriter writer,
    IReadOnlyList<BatchOutputColumn> columns,
    CancellationToken ct)
```

In the per-cell loop, pattern-match on `Transform`:

```csharp
for (var i = 0; i < columns.Count; i++)
{
    if (columns[i].Transform is not { } transform)
    {
        writer.WriteCellSpan(i, reader.GetCellSpan(i));
        continue;
    }
    var span = transform switch
    {
        FillSpec fill => fill.Value.AsSpan(),
        _             => throw new UnreachableException($"Unhandled CellTransformSpec: {transform.GetType().Name}"),
    };
    writer.WriteCellSpan(i, span);
}
```

When `TimestampFormatSpec` is added in the future, a new arm is added to the switch — no other changes needed.

### FormatDispatcherGenerator

Pass `outputSchema.Columns` instead of `outputSchema.Columns.Count`:

```csharp
return await RecordProcessor.ProcessAsync<TReader, TWriter>(
    reader, writer, outputSchema.Columns, ct)
    .ConfigureAwait(false);
```

### MorphActionParser

```csharp
"Fill" => ParseFillAction(fields),
```

```csharp
private static Result<MorphAction> ParseFillAction(Dictionary<string, string> fields)
{
    if (!fields.TryGetValue("columnName", out var columnName))
        return Results.Failure<MorphAction>("Missing required field 'columnName' for fill action");
    if (!fields.TryGetValue("value", out var value))
        return Results.Failure<MorphAction>("Missing required field 'value' for fill action");
    return Results.Success<MorphAction>(new FillColumnAction { ColumnName = columnName, Value = value });
}
```

### RecipeYamlSerializer

```csharp
case FillColumnAction fill:
    sb.AppendLine("  - type: Fill");
    sb.Append("    columnName: ").AppendLine(QuoteString(fill.ColumnName));
    sb.Append("    value: ").AppendLine(QuoteString(fill.Value));
    break;
```
