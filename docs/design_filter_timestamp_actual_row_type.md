# Design: Filter and FormatTimestamp Actual Row Type

## In Scope

1. **Filter (CLI only)**: Resolve column type from the actual per-row value
   rather than the pre-scanned schema type.
2. **FormatTimestamp (CLI only)**: Remove the schema-time gate that rejects
   based on the pre-scanned type.
3. **Separate CLI and TUI type-resolution logic**: Replace the current
   setup where CLI and TUI share the same `FilterSpec` despite differing
   type-resolution assumptions, with a CLI-specific type-resolution path.
   The core evaluation logic (comparison operators) stays shared and
   unchanged.
4. **Deduplicate CLI batch filter-related code**: As part of the above
   change, consolidate the thin wrappers and duplicated logic scattered
   across the CLI layer.

## Out of Scope

1. **TUI path**: No changes to display or filter behavior.
2. **Parse-error behavior** (e.g., the FormatTimestamp crash on parse
   failure): Unchanged; tracked separately elsewhere.
3. **Utility consolidation beyond what this change directly requires**:
   Related but independently addressable cleanups are deferred.
4. **Unifying JSON value extraction**: Decoupling from the TUI-display-
   oriented formatting (`ExtractCell`), removing the `"<null>"`/`"<error>"`
   sentinels, etc. Related but independent improvement, deferred as a future
   refactor candidate.

## Implementation Phases

### Phase 1: Refactoring (no behavior change)

Covers In-Scope item 4 only: deduplicate CLI batch filter-related code.
Item 3 (separate CLI and TUI type-resolution logic) moves to Phase 2, since
introducing a CLI-specific spec type without also wiring its resolution
logic would leave an inconsistent intermediate state. Phase 1 restructures
code only; observable behavior for both CLI and TUI stays identical to
today. `FilterSpec` is used unchanged throughout this phase.

#### Dismantle `App/Cli/FilterEvaluator.cs`

`App/Cli/FilterEvaluator.cs` is deleted. Its logic is absorbed into the two
readers, which already own the filter-invocation call site.

**File**: `src/App/Cli/CsvRecordReader.cs`

```csharp
private readonly IReadOnlyList<FilterSpec> _filters; // unchanged type in this phase

public readonly bool EvaluateFilters()
{
    ThrowIfDisposed();
    if (_reader is null)
    {
        return false;
    }

    foreach (var filter in _filters)
    {
        if (filter.SourceColumnIndex >= _reader.Current.ColCount)
        {
            return false;
        }

        var valueSpan = _reader.Current[filter.SourceColumnIndex].Span;
        if (!FilterEvaluator.EvaluateFilter(valueSpan, filter))
        {
            return false;
        }
    }

    return true;
}
```

**File**: `src/App/Cli/JsonLinesRecordReader.cs`

```csharp
private readonly IReadOnlyList<FilterSpec> _filters; // unchanged type in this phase

public readonly bool EvaluateFilters()
{
    ThrowIfDisposed();

    foreach (var filter in _filters)
    {
        if (!_filterIndexToNameBytes.TryGetValue(filter.SourceColumnIndex, out var sourceColNameBytes))
        {
            continue;
        }

        var value = JsonObjectCellExtractor.ExtractCell(_currentLineBytes.Span, sourceColNameBytes.Span);

        if (value == "<null>" || value == "<error>")
        {
            return false;
        }

        if (!FilterEvaluator.EvaluateFilter(value.AsSpan(), filter))
        {
            return false;
        }
    }

    return true;
}
```

`FilterEvaluator` in both call sites now refers to
`Refedle.Engine.Filtering.FilterEvaluator` directly (no more `App/Cli`
wrapper or `EngineFilterEvaluator` alias). The `"<null>"`/`"<error>"` guard
moves as-is; not modified in this phase (see Out of Scope).

#### Move `IsWhiteSpace`

**File**: `src/Engine/Utilities/StringUtility.cs` (new)

```csharp
namespace Refedle.Engine.Utilities;

public static class StringUtility
{
    public static bool IsWhiteSpace(ReadOnlySpan<byte> span)
    {
        foreach (var b in span)
        {
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }
}
```

`JsonLinesRecordReader.MoveNextAsync` calls `StringUtility.IsWhiteSpace`
instead of the removed `FilterEvaluator.IsWhiteSpace`.

#### Affected Files (Phase 1)

| File | Change |
|---|---|
| `src/App/Cli/FilterEvaluator.cs` | Deleted |
| `src/App/Cli/CsvRecordReader.cs` | Absorbs `EvaluateCsvFilters` logic |
| `src/App/Cli/JsonLinesRecordReader.cs` | Absorbs `EvaluateJsonFilters` logic |
| `src/Engine/Utilities/StringUtility.cs` | New; houses relocated `IsWhiteSpace` |

### Phase 2: Type Resolution Logic (behavior change)

Covers In-Scope items 1, 2, and 3: Filter resolves type from the actual
per-row value, the FormatTimestamp schema-time gate is removed, and the
CLI/TUI type-resolution separation started structurally in Phase 1 is
completed by introducing `BatchFilterSpec`.

#### New Type: `BatchFilterSpec`

**File**: `src/Engine/Filtering/BatchFilterSpec.cs`

```csharp
namespace Refedle.Engine.Filtering;

/// <summary>
/// Resolved filter specification used internally by the CLI batch pipeline
/// (<see cref="IRecordReader"/> implementations). Unlike <see cref="FilterSpec"/>
/// (TUI), carries a <see cref="ComparisonType"/> instead of a pre-resolved
/// <see cref="ColumnType"/>, so the CLI resolves type per row instead of
/// trusting the schema scan.
/// </summary>
public readonly record struct BatchFilterSpec(
    int SourceColumnIndex,
    ComparisonType ComparisonType,
    FilterOperator Operator,
    string Value
);
```

`FilterSpec` itself is unchanged — it remains the TUI-only type consumed by
`IFilterRowIndexer` implementations.

#### `FilterEvaluator` Changes

**File**: `src/Engine/Filtering/FilterEvaluator.cs`

```csharp
public static bool EvaluateFilter(ReadOnlySpan<char> rawValue, FilterSpec spec) =>
    Evaluate(rawValue, spec.Operator, spec.Value.AsSpan(), spec.ColumnType);

public static bool EvaluateFilter(ReadOnlySpan<char> rawValue, BatchFilterSpec spec)
{
    var resolvedColumnType = spec.ComparisonType switch
    {
        ComparisonType.Text => ColumnType.Text,
        ComparisonType.Number => TypeInferrer.TryParseWholeNumber(rawValue, out _)
            ? ColumnType.WholeNumber
            : ColumnType.FloatingPoint,
        ComparisonType.Timestamp => ColumnType.Timestamp,
        _ => throw new UnreachableException($"Unhandled ComparisonType: {spec.ComparisonType}"),
    };

    return Evaluate(rawValue, spec.Operator, spec.Value.AsSpan(), resolvedColumnType);
}

private static bool Evaluate(
    ReadOnlySpan<char> rawValue,
    FilterOperator op,
    ReadOnlySpan<char> specValue,
    ColumnType columnType)
{
    // body identical to today's EvaluateFilter(ReadOnlySpan<char>, FilterSpec)
}
```

The existing `EvaluateFilter(ReadOnlySpan<char>, FilterSpec)` body moves
into the new private `Evaluate` unchanged. `IsStringOperator`/
`EvaluateStringOperator`/`EvaluateNumericLong`/`EvaluateNumericDouble`/
`EvaluateTimestamp` are untouched — resolution is the only new logic.
Numeric/integer detection reuses the existing
`Refedle.Engine.IO.Csv.TypeInferrer.TryParseWholeNumber`.

#### `ActionApplier.ApplyFilter` Changes

**File**: `src/Engine/ActionApplier.cs`

```csharp
private static Result ApplyFilter(
    FilterAction filter,
    List<(string Name, ColumnType Type, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex,
    List<BatchFilterSpec> filterSpecs)
{
    if (!nameToWorkingIndex.TryGetValue(filter.ColumnName, out var idx))
    {
        return Results.Success();
    }

    var (_, _, columnIndex, _) = workingColumns[idx];
    filterSpecs.Add(
        new BatchFilterSpec(
            SourceColumnIndex: columnIndex,
            ComparisonType: filter.ComparisonType,
            Operator: filter.Operator,
            Value: filter.Value
        )
    );
    return Results.Success();
}
```

The schema-scanned `type` is no longer read for filter purposes;
`filter.ComparisonType` (already present on `FilterAction`) is the
resolution source instead.

#### `ActionApplier.ApplyFormatTimestamp` Changes

**File**: `src/Engine/ActionApplier.cs`

```csharp
private static Result ApplyFormatTimestamp(
    FormatTimestampAction formatTimestamp,
    List<(string Name, ColumnType Type, int ColumnIndex, string OutputName)> workingColumns,
    Dictionary<string, int> nameToWorkingIndex,
    Dictionary<int, CellTransformSpec> transformsByWorkingIndex)
{
    if (!nameToWorkingIndex.TryGetValue(formatTimestamp.ColumnName, out var idx))
    {
        return Results.Success();
    }

    transformsByWorkingIndex[idx] = new TimestampFormatSpec(formatTimestamp.TargetFormat);
    return Results.Success();
}
```

The schema-time gate (`if (type != ColumnType.Timestamp) return
Results.Failure(...)`) is removed. `RecordProcessor.ApplyTimestampFormat`
is unchanged — it already does per-row `DateTime.TryParse` and throws
`FormatException` on failure (existing crash behavior; see Out of Scope).

#### `BatchOutputSchema` Changes

**File**: `src/Engine/BatchOutputSchema.cs`

```csharp
public sealed record BatchOutputSchema(
    IReadOnlyList<BatchOutputColumn> Columns,
    IReadOnlyList<BatchFilterSpec> Filters);
```

#### Readers: switch to `BatchFilterSpec`

**Files**: `src/App/Cli/CsvRecordReader.cs`, `src/App/Cli/JsonLinesRecordReader.cs`

```csharp
private readonly IReadOnlyList<BatchFilterSpec> _filters; // was FilterSpec after Phase 1
```

No other changes to `EvaluateFilters()`'s body from Phase 1 — only the
element type of `_filters` changes; the same `FilterEvaluator.EvaluateFilter`
call site now resolves to the new `BatchFilterSpec` overload.

#### Affected Files (Phase 2)

| File | Change |
|---|---|
| `src/Engine/Filtering/BatchFilterSpec.cs` | New CLI-only spec type (`ComparisonType`-based) |
| `src/Engine/Filtering/FilterEvaluator.cs` | Extract shared `Evaluate`; add `BatchFilterSpec` overload with per-row resolution |
| `src/Engine/ActionApplier.cs` | `ApplyFilter` builds `BatchFilterSpec` from `filter.ComparisonType`; `ApplyFormatTimestamp` drops the schema-time gate |
| `src/Engine/BatchOutputSchema.cs` | `Filters` type changes to `IReadOnlyList<BatchFilterSpec>` |
| `src/App/Cli/CsvRecordReader.cs` | `_filters` field type changes to `BatchFilterSpec` |
| `src/App/Cli/JsonLinesRecordReader.cs` | `_filters` field type changes to `BatchFilterSpec` |
