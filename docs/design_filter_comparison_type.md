# Design: Explicit Comparison Type for FilterAction Recipe Format

## Requirements

- Allow a recipe to explicitly declare which type (`ComparisonType`) a `FilterAction`'s value should be interpreted as.
- This information must round-trip consistently across the model, parser, serializer, and TUI dialog.
- Validate both the operator/comparison-type combination and the value itself, at recipe load time and at TUI input time, rejecting invalid state as early as possible.

## Out of Scope

- Making filter *evaluation* logic actually use `ComparisonType` (evaluation keeps using the existing `ColumnType`-based logic).
- `ActionApplier.ApplyFormatTimestamp` crash behavior.
- Backward compatibility / fallback for old recipes.

## New Type: `ComparisonType`

**File**: `src/Engine/Models/Actions/ComparisonType.cs`

```csharp
public enum ComparisonType
{
    Text,
    Number,
    Timestamp,
}
```

No `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` attribute (unlike `FilterOperator`) — nothing in the codebase actually serializes these enums via `System.Text.Json`, so the attribute would be dead code. `FilterOperator`'s existing attribute is left as-is; out of scope for this change.

## `FilterAction` Changes

**File**: `src/Engine/Models/Actions/FilterAction.cs`

```csharp
public sealed record FilterAction : MorphAction
{
    public string ColumnName { get; }
    public FilterOperator Operator { get; }
    public ComparisonType ComparisonType { get; }
    public string Value { get; }

    private FilterAction(string columnName, FilterOperator op, ComparisonType comparisonType, string value)
    {
        ColumnName = columnName;
        Operator = op;
        ComparisonType = comparisonType;
        Value = value;
    }

    // public: called from Refedle.App too (no InternalsVisibleTo grant from Engine to App)
    public static Result Validate(FilterOperator op, ComparisonType comparisonType, string value) { /* see tables below */ }

    public static Result<FilterAction> Create(string columnName, FilterOperator op, ComparisonType comparisonType, string value)
    {
        var result = Validate(op, comparisonType, value);
        if (result.IsFailure)
        {
            return Results.Failure<FilterAction>(result.Error);
        }

        return Results.Success(new FilterAction(columnName, op, comparisonType, value));
    }
}
```

Callers of `Create`: `MorphActionParser.ParseFilterAction` and `ColumnActionHandler` (both need the constructed `FilterAction`). `FilterColumnDialog` calls `Validate` directly at confirm time (see below) since it only needs the validation result, not a `FilterAction` instance — the dialog doesn't have a `ColumnName` to pass until `ColumnActionHandler` constructs the action afterward.

### `Validate` — Operator / ComparisonType combination

| ComparisonType | Valid operators |
|---|---|
| `Text` | `Equals`, `NotEquals`, `Contains`, `NotContains`, `StartsWith`, `EndsWith` |
| `Number` | `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `GreaterThanOrEqual`, `LessThanOrEqual` |
| `Timestamp` | `Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `GreaterThanOrEqual`, `LessThanOrEqual` |

This matches current runtime behavior: today, `FilterEvaluator` already excludes all rows (returns `false`) when a numeric/timestamp operator is applied to a `Text` column — there is no lexicographic string comparison implemented anywhere. This design turns that silent no-op into an explicit rejection at parse/input time. Extending `Text` to support ordering operators via lexicographic comparison is a possible future addition, not part of this change.

### `Validate` — `Value` format check, per `ComparisonType`

| ComparisonType | Check |
|---|---|
| `Text` | none — any string is valid |
| `Number` | `double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)` |
| `Timestamp` | `DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)` |

This check only verifies that `Value` is parseable as the declared `ComparisonType` itself. Once evaluation is wired to use `ComparisonType` (planned as a follow-up, out of scope here), this parseability check becomes the basis for the value being interpreted correctly at evaluation time.

`double.IsFinite` excludes `NaN`/`Infinity`/`-Infinity` — `double.TryParse` alone accepts these as valid doubles, but comparisons against them are never meaningful (IEEE 754 comparisons involving `NaN` are always `false`), so they are rejected at `Validate` time instead of silently producing an always-empty filter result.

`double.TryParse` alone is sufficient — no separate `long.TryParse` check is needed. `ComparisonType.Number` doesn't distinguish whole numbers from floating-point (that split only exists in the evaluation-time `ColumnType`, out of scope here). Every string `long.TryParse` accepts (`NumberStyles.Integer`) is also accepted by `double.TryParse` (`NumberStyles.Any` is a superset). Precision loss for very large integers doesn't cause `TryParse` to return `false` — only the parsed value would be imprecise, and `Validate` only checks parseability, not the parsed value itself.

## `MorphActionParser.ParseFilterAction` Changes

**File**: `src/Engine/Recipes/MorphActionParser.cs`

```csharp
private static Result<MorphAction> ParseFilterAction(Dictionary<string, string> fields)
{
    // ColumnName / Operator / Value checks unchanged

    if (!fields.TryGetValue("comparisonType", out var comparisonTypeStr))
    {
        return Results.Failure<MorphAction>("Missing required field 'comparisonType' for filter action");
    }

    if (!Enum.TryParse<ComparisonType>(comparisonTypeStr, ignoreCase: false, out var comparisonType))
    {
        return Results.Failure<MorphAction>($"Invalid enum value for comparisonType: '{comparisonTypeStr}'");
    }

    var action = FilterAction.Create(columnName, filterOperator, comparisonType, filterValue);
    if (action.IsFailure)
    {
        return Results.Failure<MorphAction>(action.Error);
    }

    return Results.Success<MorphAction>(action.Value);
}
```

`comparisonType` check placed after `value` (order doesn't affect correctness — `fields` is a `Dictionary<string, string>` built by `RecipeYamlParser` from `key: value` lines regardless of order; `TryGetValue` order only affects which error surfaces first when multiple fields are missing).

## `RecipeYamlSerializer` Changes

**File**: `src/Engine/Recipes/RecipeYamlSerializer.cs`

```csharp
case FilterAction filter:
    sb.AppendLine("  - type: filter");
    sb.Append("    columnName: ").AppendLine(QuoteString(filter.ColumnName));
    sb.AppendLine(CultureInfo.InvariantCulture, $"    operator: {filter.Operator}");
    sb.AppendLine(CultureInfo.InvariantCulture, $"    comparisonType: {filter.ComparisonType}");   // new
    sb.Append("    value: ").AppendLine(QuoteString(filter.Value));
    break;
```

`CultureInfo.InvariantCulture` matches the existing `operator`/`targetType` lines: `AnalysisLevel latest-all` + `TreatWarningsAsErrors` requires an explicit `IFormatProvider` on the interpolated-string `AppendLine` overload, so the emitted YAML doesn't vary with the host machine's locale.

## `FilterColumnDialog` Changes

**File**: `src/App/Views/Dialogs/FilterColumnDialog.cs`

```csharp
internal ComparisonType? SelectedComparisonType { get; private set; }   // new

internal FilterColumnDialog(string columnName)
{
    // ... existing colLabel / operatorLabel / selector unchanged ...

    var comparisonTypeLabel = new Label { Text = "Comparison Type:", X = 0, Y = Pos.Bottom(selector) + 1 };
    var comparisonTypeSelector = new OptionSelector<ComparisonType>
    {
        X = Pos.Right(comparisonTypeLabel) + 1,
        Y = Pos.Bottom(selector) + 1,
        Width = Dim.Fill(),
        Value = ComparisonType.Text,
    };
    comparisonTypeSelector.EnableAutoSelectAndVimKeys();

    // ... existing valueLabel / textField unchanged, but Y shifts to Pos.Bottom(comparisonTypeSelector) + 1 ...

    var errorLabel = new Label { Text = string.Empty, X = 0, Y = Pos.Bottom(textField) + 1 };

    Add(colLabel, operatorLabel, selector, comparisonTypeLabel, comparisonTypeSelector, valueLabel, textField, errorLabel);

    // ... existing okButton / cancelButton unchanged ...

    void Confirm()
    {
        if (string.IsNullOrWhiteSpace(textField.Text))
        {
            return;
        }

        errorLabel.Text = string.Empty; // clear stale error from a previous failed attempt

        var validation = FilterAction.Validate(selector.Value, comparisonTypeSelector.Value, textField.Text);
        if (validation.IsFailure)
        {
            errorLabel.Text = validation.Error;
            return; // dialog stays open
        }

        SelectedOperator = selector.Value;
        SelectedComparisonType = comparisonTypeSelector.Value;
        Value = textField.Text;
        Confirmed = true;
        App?.RequestStop();
    }

    // ... existing button/textField Accepting wiring unchanged, except:
    //     selector.Accepting now focuses comparisonTypeSelector (not textField),
    //     and comparisonTypeSelector.Accepting focuses textField
}
```

Both selectors stay independently free-choice — no dynamic filtering of operator options based on the selected comparison type. Validation runs once at confirm time via the same `FilterAction.Validate` used by the parser and constructor.

## `ColumnActionHandler` Changes

**File**: `src/App/Views/ColumnActionHandler.cs`

```csharp
var action = FilterAction.Create(rawName, dialog.SelectedOperator.Value, dialog.SelectedComparisonType.Value, dialog.Value);
if (action.IsFailure)
{
    throw new UnreachableException(action.Error); // dialog already validated at confirm time
}

onMorphAction(action.Value);
```

Replaces the current object-initializer call; the `null` guard on `dialog.Confirmed || dialog.SelectedOperator is null || dialog.Value is null` gains `|| dialog.SelectedComparisonType is null`.

## Architecture Decision Log

### ADR-1: Why `ComparisonType` is declared explicitly in the recipe

**Context**

Evaluating a `Filter` action's ordering operators (`GreaterThan`, etc.) requires interpreting the raw value as a number or timestamp. Two approaches were considered for how to determine this type.

**Options**

- **A — Pre-scan all rows to determine the type**: read the entire target column before filtering, then apply a consistent comparison. Accurate and uniform across rows, but incurs a full-file read on every filter application — at odds with this project's streaming design.
- **B — Infer the type from each row's raw value on the fly**: no upfront scan needed, streaming-friendly, but if a column genuinely contains mixed types (row 1 numeric, row 2 timestamp-like — considered unlikely in practice but not impossible), the comparison logic applied would vary row by row.

**Decision**

Add an explicit `ComparisonType` (`Text` / `Number` / `Timestamp`) field to `FilterAction`. The type is decided **once** — either hand-authored in the recipe, or chosen by the user in the TUI — and applied uniformly to every row of the target column during evaluation.

Wiring evaluation to actually dispatch by `ComparisonType` is planned as a follow-up issue, not addressed here (see Out of Scope).

**Rationale**

This avoids the downsides of both options: no full-file pre-scan is required (avoiding Option A's overhead), and the comparison logic never varies row to row (avoiding Option B's consistency risk). The type decision is baked into the recipe as a single, one-time judgment made at recipe-authoring time.

Declaring `ComparisonType` explicitly also removes the need for any fallback logic on comparison failure (e.g., try `Number`, and if that fails try `Timestamp`, and if that fails fall back to `Text`) — an arbitrary heuristic that Option B would otherwise require. With the type known upfront, evaluation simply parses the value once as the declared type; a parse failure just excludes that row (fail-closed, consistent with the Consequence below).

`ComparisonType` is a distinct type from `ColumnType` (rather than reusing it) because their scopes differ. `ComparisonType` is scoped to `FilterAction` alone and only needs the three values meaningful for a filter comparison (`Text` / `Number` / `Timestamp`). `ColumnType`, by contrast, is used app-wide (table display, `Cast`, the full schema) and includes values with no meaning for filter comparison (`JsonObject`, `JsonArray`, etc.). Reusing `ColumnType` on `FilterAction` would make those irrelevant values look like valid input.

**Consequence**

- A row whose value doesn't parse as the declared `ComparisonType` is excluded from the result (fail-closed). No fallback to a different type, no exception.
- `ComparisonType` is intentionally independent from the target column's actual scanned `ColumnType`. Schema scanning (the first 200 rows for the CLI; the TUI additionally continues scanning in the background and may update the schema later) is only a best-effort snapshot, not a guarantee over the full file — so a user setting `ComparisonType` to something that disagrees with the scanned `ColumnType` is a legitimate choice, not an error.

### ADR-2: Why `FilterColumnDialog` doesn't auto-determine or default `ComparisonType` from the column's type

**Context**

When building a `Filter` condition on a column in the TUI, how should `ComparisonType` be selected? The column's inferred type (`ColumnType`) is already known from schema scanning and reachable via `AppState.Schema`. Several ways of using it were considered.

**Options**

- **A — Fully automatic**: remove the `ComparisonType` selector entirely; derive it automatically from `ColumnType`. The user never chooses.
- **B — Smart default**: keep the selector, but pre-select the value derived from `ColumnType`. The user can still change it.
- **C — Static default**: the selector always starts at `ComparisonType.Text`, independent of `ColumnType`. The user chooses freely.

**Decision**

Adopt Option C.

**Rationale**

Both A and B would need to resolve the column's *effective* type at that point in the action stack (accounting for any preceding `CastColumnAction`), not just the raw schema-scanned type — this is meaningfully more implementation cost than it's worth right now. A static default (C) needs no additional wiring and preserves full user choice. Automatic derivation can be added later if the wiring cost becomes worthwhile.

**Consequence**

- A user can pick `ComparisonType.Number` even on a column inferred as `Text`. Rows whose value doesn't parse under that type are excluded (fail-closed) — the same behavior as ADR-1's consequence.

## Affected Files

| File | Change |
|---|---|
| `src/Engine/Models/Actions/ComparisonType.cs` | New enum |
| `src/Engine/Models/Actions/FilterAction.cs` | Add `ComparisonType` field; switch to explicit constructor; add `Validate` |
| `src/Engine/Recipes/MorphActionParser.cs` | Parse + validate `comparisonType` in `ParseFilterAction` |
| `src/Engine/Recipes/RecipeYamlSerializer.cs` | Emit `comparisonType` |
| `src/App/Views/Dialogs/FilterColumnDialog.cs` | Add `ComparisonType` selector, error label, confirm-time validation |
| `src/App/Views/ColumnActionHandler.cs` | Update `FilterAction` construction call site |
| `tests/Refedle.Tests/Engine/Models/Actions/FilterActionTests.cs` | `Validate`/`Create` test cases |
| `tests/Refedle.Tests/Engine/Recipes/MorphActionParserTests.cs` | Parse success/failure cases for `comparisonType` |
| `tests/Refedle.Tests/Engine/Recipes/RecipeYamlSerializerTests.cs` | Round-trip includes `comparisonType` |
| `tests/Refedle.Tests/App/Views/Dialogs/FilterColumnDialogTests.cs` | Confirm-time validation, error label behavior |
| `tests/Refedle.Tests/App/Views/ColumnActionHandlerTests.cs` | Updated construction call site |

## Notable Test Cases

- **`FilterAction.Validate`**: every invalid operator/comparisonType pair from the combination table; every valid pair with an unparseable `Value` for `Number`/`Timestamp`; `NaN`/`Infinity`/`-Infinity` rejected for `Number`; every valid pair with a parseable, finite `Value` succeeds; `Text` accepts any `Value`.
- **`FilterAction.Create`**: returns a failure `Result` with `Validate`'s error when `Validate` would fail; returns a success `Result` wrapping the constructed `FilterAction` otherwise.
- **`MorphActionParser.ParseFilterAction`**: missing `comparisonType` → failure; invalid enum string → failure; valid combination with an unparseable `Value` → failure; fully valid recipe → success.
- **`RecipeYamlSerializer`**: serialize → parse round-trip preserves `ComparisonType`.
- **`FilterColumnDialog`**: Confirm with an invalid operator/comparisonType combination keeps the dialog open and sets the error label; Confirm with an unparseable `Value` does the same; Confirm with valid input sets `Confirmed`/`SelectedComparisonType` and closes the dialog; a stale error from a previous failed attempt is cleared before each validation.
- **`ColumnActionHandler`**: `HandleFilterColumn` constructs `FilterAction` with the dialog's selected `ComparisonType`.
